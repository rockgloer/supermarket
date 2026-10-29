using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// Ход игры: меню (СТАРТ на планшете) -> покупатель приходит -> называет заказ -> игрок собирает его в корзину ->
// ставит корзину на кассу (сканирование, звук) -> покупатель платит купюрой -> игрок кладёт купюру в кассу
// (звон, ящик, искры) -> следующий покупатель. После последнего: «Задача выполнена» и итог по баллам.
public class TaskManager : MonoBehaviour
{
    public Customer[] customers;
    public Zone counterZone;     // сюда ставим корзину
    public Zone registerZone;    // сюда кладём купюру
    public GameObject moneyTemplate;
    public Label board;
    public Label wrist;          // текст на «браслете» над левой рукой
    public GameUI ui;            // планшет-интерфейс
    public ParticleSystem payBurst;   // искры при оплате
    public Animator drawer;      // ящик кассы
    public Vector3[] pathIn;     // маршрут покупателя от входа до кассы (первая точка — где он появляется)
    public Vector3[] pathOut;    // маршрут к выходу
    public Vector3 cashierPos, moneySpawnPos;

    enum State { Menu, Intro, Walking, Collecting, Paying, Leaving, Between, Done }
    State state = State.Menu;
    int index, score, errors, errorsThis, served;
    Customer cur;
    float timer = 2f, customerTime;
    string lastSignature = "";
    bool prevPrimary;

    bool RestartPressed()
    {
        var kb = Keyboard.current;
        bool key = kb != null && kb.rKey.wasPressedThisFrame;
        bool p = XRInput.Primary(XRNode.RightHand) || XRInput.Primary(XRNode.LeftHand);
        bool btn = p && !prevPrimary;
        prevPrimary = p;
        return key || btn;
    }

    void Start()
    {
        SetBoard("ДОБРО ПОЖАЛОВАТЬ!\nВы кассир супермаркета.\nНажмите СТАРТ на планшете\n(дотроньтесь рукой, клавиша E\nили R / кнопка A)");
        SetWrist("Нажмите СТАРТ\nна планшете");
        if (ui != null)
        {
            ui.StartPressed += BeginGame;
            ui.RestartPressed += Restart;
            ui.ShowMenu();
            ui.SetProgress(0, customers.Length);
        }
        else BeginGame();
    }

    void OnDestroy()
    {
        if (ui != null) { ui.StartPressed -= BeginGame; ui.RestartPressed -= Restart; }
    }

    void BeginGame()
    {
        if (state != State.Menu) return;
        score = 0; errors = 0; served = 0; index = 0;
        timer = 2f;
        state = State.Intro;
        if (ui != null) { ui.BeginGame(customers.Length); ui.SetScore(0); }
        SetBoard("ДОБРО ПОЖАЛОВАТЬ!\nСкоро придёт первый покупатель.");
        SetWrist("Ждём первого\nпокупателя");
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Update()
    {
        bool pressed = RestartPressed();
        switch (state)
        {
            case State.Menu:
                if (pressed) { if (ui != null) { SoundManager.Click(); } BeginGame(); }
                break;
            case State.Intro:
            case State.Between:
                timer -= Time.deltaTime;
                if (timer <= 0f) StartCustomer();
                break;
            case State.Walking:
                if (cur.Arrived)
                {
                    cur.FaceTowards(cashierPos);
                    cur.Say("Здравствуйте!");
                    SoundManager.Hello(cur.transform.position + Vector3.up);
                    SetBoard("ЗАДАНИЕ " + (index + 1) + "/" + customers.Length + "\nСоберите заказ в корзину\nи поставьте её на кассу:\n" + OrderText(cur.order));
                    SetWrist("ЗАКАЗ " + (index + 1) + "/" + customers.Length + "\n" + OrderText(cur.order));
                    if (ui != null) ui.SetStatus("Собрать заказ: " + cur.displayName);
                    customerTime = 0f;
                    errorsThis = 0;
                    state = State.Collecting;
                }
                break;
            case State.Collecting:
                customerTime += Time.deltaTime;
                CheckBasket();
                break;
            case State.Paying:
                CheckMoney();
                break;
            case State.Leaving:
                if (cur.Arrived)
                {
                    cur.gameObject.SetActive(false);
                    index++;
                    if (index >= customers.Length) Finish();
                    else
                    {
                        timer = 2f;
                        state = State.Between;
                    }
                }
                break;
            case State.Done:
                if (pressed) Restart();
                break;
        }
    }

    void Finish()
    {
        string summary = "Готово! Баллы: " + score + ", ошибок: " + errors;
        SetBoard("ЗАДАЧА ВЫПОЛНЕНА!\nВсе покупатели обслужены.\nБаллы: " + score + "   Ошибок: " + errors + "\nR, кнопка A или ЗАНОВО на планшете");
        SetWrist("ЗАДАЧА\nВЫПОЛНЕНА!");
        if (ui != null) ui.ShowDone(summary);
        SoundManager.Fanfare();
        if (payBurst != null) payBurst.Play();
        state = State.Done;
    }

    void StartCustomer()
    {
        cur = customers[index];
        cur.transform.position = pathIn[0];
        cur.gameObject.SetActive(true);
        cur.Say("");
        cur.GoPath(pathIn, 1);
        lastSignature = "";
        SetBoard("Покупатель " + (index + 1) + "/" + customers.Length + " входит в магазин...");
        SetWrist("Идёт покупатель " + (index + 1) + "/" + customers.Length);
        state = State.Walking;
    }

    void CheckBasket()
    {
        foreach (var basket in counterZone.Find<BasketContents>())
        {
            if (basket.Grab.Holder != null) continue; // корзина в руке — ждём
            var found = new List<Product>();
            var counts = basket.GetCounts(found);
            if (counts.Count == 0) continue;

            if (Matches(counts, cur.order))
            {
                StartCoroutine(ScanBeeps(found.Count));
                foreach (var p in found) Destroy(p.gameObject); // «пробили» товары
                cur.Say("Спасибо!");
                SpawnMoney();
                SetBoard("Заказ верный!\nВозьмите купюру\nи положите её в кассу.");
                SetWrist("Возьмите купюру\nи положите в кассу");
                if (ui != null) ui.SetStatus("Возьмите купюру -> в кассу");
                state = State.Paying;
                return;
            }

            string sig = Signature(counts);
            if (sig != lastSignature)
            {
                lastSignature = sig;
                cur.Say("Это не то!");
                errors++;
                errorsThis++;
                SoundManager.Error();
                if (ui != null) ui.SetStatus("Не тот набор товаров");
            }
        }
    }

    IEnumerator ScanBeeps(int n)
    {
        Vector3 p = counterZone.transform.position;
        for (int i = 0; i < n; i++)
        {
            SoundManager.Beep(p);
            yield return new WaitForSeconds(0.14f);
        }
    }

    void CheckMoney()
    {
        foreach (var m in registerZone.Find<Money>())
        {
            var g = m.GetComponent<Grabbable>();
            if (g != null && g.Holder != null) continue;
            Destroy(m.gameObject);
            cur.Say("Хорошего дня!");
            cur.GoPath(pathOut, 0);

            // эффекты оплаты: звон, ящик кассы, искры
            Vector3 rp = registerZone.transform.position;
            SoundManager.Ching(rp);
            SoundManager.Drawer(rp);
            if (drawer != null) drawer.SetTrigger("open");
            if (payBurst != null)
            {
                payBurst.transform.position = rp + Vector3.up * 0.1f;
                payBurst.Play();
            }

            served++;
            int pts = Mathf.Max(40, 150 - Mathf.RoundToInt(customerTime)) - errorsThis * 10;
            score += Mathf.Max(10, pts);
            if (ui != null) { ui.SetProgress(served, customers.Length); ui.SetScore(score); ui.SetStatus("Оплата принята"); }
            SetBoard("Оплата принята.\nПокупатель " + (index + 1) + "/" + customers.Length + " обслужен.");
            SetWrist("Оплата принята");
            state = State.Leaving;
            return;
        }
    }

    void SpawnMoney()
    {
        var m = Instantiate(moneyTemplate, moneySpawnPos, Quaternion.identity);
        m.SetActive(true);
    }

    static bool Matches(Dictionary<string, int> counts, string[] order)
    {
        var need = new Dictionary<string, int>();
        foreach (var o in order)
        {
            if (need.ContainsKey(o)) need[o]++;
            else need[o] = 1;
        }
        if (need.Count != counts.Count) return false;
        foreach (var kv in need)
        {
            int have;
            if (!counts.TryGetValue(kv.Key, out have) || have != kv.Value) return false;
        }
        return true;
    }

    static string Signature(Dictionary<string, int> counts)
    {
        var keys = new List<string>(counts.Keys);
        keys.Sort();
        var sb = new StringBuilder();
        foreach (var k in keys) sb.Append(k).Append('=').Append(counts[k]).Append(';');
        return sb.ToString();
    }

    static string OrderText(string[] order)
    {
        var counts = new Dictionary<string, int>();
        var names = new List<string>();
        foreach (var o in order)
        {
            if (!counts.ContainsKey(o)) { counts[o] = 0; names.Add(o); }
            counts[o]++;
        }
        var sb = new StringBuilder();
        foreach (var n in names) sb.Append("- ").Append(n).Append(" x").Append(counts[n]).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }

    void SetBoard(string t)
    {
        if (board != null) board.SetText(t);
    }

    void SetWrist(string t)
    {
        if (wrist != null) wrist.SetText(t);
    }
}
