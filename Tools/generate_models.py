# Генератор моделей и текстур проекта. Запуск: python3 generate_models.py
# Метры, Y вверх, начало координат = низ-центр, "лицо" смотрит в +Z.
import os, math
from meshlib import *
import texlib as T

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MODELS = os.path.join(ROOT, "Assets", "Models")
TEX = os.path.join(ROOT, "Assets", "Textures")
os.makedirs(MODELS, exist_ok=True); os.makedirs(TEX, exist_ok=True)
def mp(n): return os.path.join(MODELS, n + ".obj")
def tp(n): return os.path.join(TEX, n + ".png")

# ------------------------------------------------------------------ интерьер
shelf = []
for sx in (-0.98,0.98): shelf.append(box(sx,1.0,0,0.04,2.0,0.5))
for y in (0.12,0.57,1.02,1.47): shelf.append(box(0,y,0,1.96,0.04,0.5))
shelf.append(box(0,1.98,0,2.0,0.04,0.5))
shelf.append(box(0,1.0,-0.24,1.96,1.9,0.02))     # задняя стенка (тонкая)
write_obj(mp("Shelf"),"Shelf",shelf)

basket = [box(0,0.01,0,0.5,0.02,0.34)]
for z in (-0.16,0.16): basket.append(box(0,0.12,z,0.5,0.22,0.02))
for x in (-0.24,0.24): basket.append(box(x,0.12,0,0.02,0.22,0.30))
for x in (-0.27,0.27): basket.append(box(x,0.2,0,0.04,0.02,0.12))
write_obj(mp("Basket"),"Basket",basket)

write_obj(mp("Counter"),"Counter",[box(0,0.46,0,2.3,0.92,0.7), box(0,0.94,0,2.4,0.04,0.8)])
write_obj(mp("Register"),"Register",[box(0,0.08,0,0.4,0.16,0.4),box(0,0.18,0.08,0.3,0.04,0.2),box(0,0.26,-0.1,0.04,0.2,0.04),box(0,0.34,-0.08,0.26,0.16,0.03)])
st=[]
for i in range(20):
    top=(i+1)*0.175
    st.append(box(0, top/2, i*0.28+0.14, 2.0, top, 0.28))
write_obj(mp("Stairs"),"Stairs",st)
write_obj(mp("Plant"),"Plant",[cylinder(0,0.2,0,0.18,0.4,14), ellipsoid(0,0.75,0,0.32,0.35,0.32,seg=14,rings=8), ellipsoid(0.1,1.05,0.05,0.22,0.25,0.22,seg=12,rings=8)])

# ------------------------------------------------------------------ товары (продаются)
FULL=None
write_obj(mp("Milk"),"Milk",[box(0,0.11,0,0.08,0.22,0.08), box(0,0.235,0,0.04,0.03,0.04)])
write_obj(mp("Bread"),"Bread",[cylinder(0,0.055,0,0.055,0.26,14,axis='x')])
write_obj(mp("Cola"),"Cola",[cylinder(0,0.06,0,0.033,0.12,16), cylinder(0,0.125,0,0.028,0.012,16)])
write_obj(mp("Apple"),"Apple",[ellipsoid(0,0.04,0,0.04,0.038,0.04,seg=16,rings=10)])
write_obj(mp("Soap"),"Soap",[box(0,0.015,0,0.09,0.03,0.06)])
write_obj(mp("Cereal"),"Cereal",[box(0,0.12,0,0.16,0.24,0.06)])
write_obj(mp("Banknote"),"Banknote",[box(0,0.001,0,0.16,0.002,0.07)])

T.pack_texture(tp("Tex_Milk"),"МОЛОКО",(0.95,0.96,1.0),(0.25,0.5,0.9),(0.1,0.25,0.6))
T.pack_texture(tp("Tex_Cola"),"COLA",(0.85,0.1,0.1),(0.95,0.95,0.95),(0.8,0.05,0.05),wrap=True)
T.pack_texture(tp("Tex_Soap"),"МЫЛО",(0.55,0.82,0.95),(1,1,1),(0.1,0.4,0.6))
T.pack_texture(tp("Tex_Cereal"),"ХЛОПЬЯ",(0.98,0.78,0.2),(0.85,0.3,0.1),(1,1,1))
T.bread_texture(tp("Tex_Bread"))
T.fruit_texture(tp("Tex_Apple"),(0.9,0.15,0.1),(0.45,0.55,0.1))
T.money_texture(tp("Tex_Banknote"))
T.floor_texture(tp("Tex_Floor"))

# ------------------------------------------------------------------ декоративные товары (только для вида)
write_obj(mp("DCarton"),"DCarton",[box(0,0.1,0,0.07,0.2,0.07), box(0,0.21,0,0.035,0.02,0.035)])
write_obj(mp("DBottle"),"DBottle",[lathe([(0.0,0.0),(0.03,0.0),(0.032,0.02),(0.032,0.14),(0.025,0.19),(0.012,0.22),(0.012,0.25),(0.0,0.25)],seg=16,caps=False)])
write_obj(mp("DCan"),"DCan",[cylinder(0,0.06,0,0.033,0.12,16)])
write_obj(mp("DBox"),"DBox",[box(0,0.13,0,0.19,0.26,0.06)])
write_obj(mp("DBag"),"DBag",[box(0,0.125,0,0.17,0.25,0.045)])
write_obj(mp("DJar"),"DJar",[cylinder(0,0.045,0,0.04,0.09,16), cylinder(0,0.095,0,0.042,0.02,16)])
write_obj(mp("DPack"),"DPack",[box(0,0.05,0,0.2,0.1,0.1)])
write_obj(mp("DTall"),"DTall",[box(0,0.12,0,0.11,0.24,0.07), cylinder(0,0.255,0,0.02,0.03,12)])

DECOR = [  # модель, подпись, фон, полоса, цвет текста
 ("DCarton","СОК",(1.0,0.65,0.1),(1,1,1),(0.8,0.3,0.0)),
 ("DCarton","КЕФИР",(0.96,0.97,0.9),(0.2,0.65,0.3),(0.1,0.4,0.15)),
 ("DCarton","ТОМАТЫ",(0.9,0.2,0.15),(1,0.9,0.8),(0.6,0.05,0.05)),
 ("DBottle","ВОДА",(0.55,0.8,0.95),(0.2,0.45,0.85),(1,1,1)),
 ("DBottle","ЛИМОНАД",(0.95,0.9,0.3),(0.3,0.7,0.2),(0.2,0.45,0.1)),
 ("DBottle","МАСЛО",(0.95,0.75,0.2),(0.7,0.45,0.05),(1,1,1)),
 ("DCan","ТОНИК",(0.15,0.65,0.65),(1,1,1),(0.05,0.4,0.4)),
 ("DCan","КВАС",(0.45,0.25,0.1),(0.9,0.7,0.3),(1,0.95,0.85)),
 ("DCan","СОДА",(0.5,0.25,0.7),(0.9,0.85,1),(0.35,0.1,0.55)),
 ("DBox","ПЕЧЕНЬЕ",(0.6,0.35,0.15),(0.95,0.8,0.5),(0.4,0.2,0.05)),
 ("DBox","МЮСЛИ",(0.95,0.6,0.15),(0.4,0.7,0.2),(1,1,1)),
 ("DBox","ЧАЙ",(0.2,0.55,0.3),(0.95,0.9,0.5),(0.1,0.35,0.15)),
 ("DBox","КАКАО",(0.4,0.22,0.12),(1,0.9,0.7),(0.35,0.15,0.05)),
 ("DBag","ЧИПСЫ",(0.95,0.8,0.1),(0.85,0.15,0.1),(1,1,1)),
 ("DBag","РИС",(0.96,0.95,0.9),(0.85,0.7,0.3),(0.5,0.35,0.05)),
 ("DBag","САХАР",(0.95,0.97,1.0),(0.3,0.5,0.9),(0.1,0.25,0.6)),
 ("DBag","МУКА",(0.93,0.88,0.75),(0.6,0.45,0.25),(0.4,0.25,0.1)),
 ("DJar","ВАРЕНЬЕ",(0.7,0.12,0.15),(0.98,0.9,0.8),(0.55,0.05,0.08)),
 ("DJar","МЁД",(0.95,0.7,0.1),(1,0.95,0.7),(0.6,0.35,0.0)),
 ("DJar","ОГУРЦЫ",(0.4,0.6,0.2),(0.95,0.95,0.8),(0.2,0.4,0.05)),
 ("DPack","МАКАРОНЫ",(0.98,0.85,0.25),(0.85,0.2,0.1),(1,1,1)),
 ("DPack","ВАФЛИ",(0.95,0.55,0.7),(1,1,1),(0.7,0.15,0.4)),
 ("DPack","КОНФЕТЫ",(0.75,0.2,0.65),(1,0.85,0.2),(1,1,1)),
 ("DTall","ПОРОШОК",(0.2,0.45,0.85),(1,1,1),(0.1,0.25,0.65)),
 ("DTall","ШАМПУНЬ",(0.55,0.3,0.75),(0.95,0.8,1),(0.4,0.15,0.6)),
 ("DTall","ГЕЛЬ",(0.2,0.7,0.7),(0.9,1,1),(0.05,0.4,0.45)),
]
cyl_models={"DCan","DJar","DBottle"}
for i,(m,label,bg,band,fg) in enumerate(DECOR):
    T.pack_texture(tp("Decor_%s_%02d"%(m,i)),label,bg,band,fg,wrap=(m in cyl_models))


# ------------------------------------------------------------------ руки: текстуры (сама сетка — hand_skin.py)
from PIL import Image
def hand_tex():
    # Tex_Hand — тёмные матовые руки (как в Oculus Home), Tex_HandSkin — светлая кожа с красным манжетом
    img=Image.new("RGB",(256,128))
    T.fill_noise(img,(0,0,256,128),(0.075,0.075,0.085),amp=2,scale=10)
    img.save(tp("Tex_Hand"))
    img2=Image.new("RGB",(256,128))
    T.fill_noise(img2,(0,0,128,128),(0.93,0.76,0.63),amp=5,scale=8)
    T.fill_noise(img2,(128,0,256,128),(0.80,0.16,0.20),amp=6,scale=4)
    img2.save(tp("Tex_HandSkin"))
hand_tex()

# ------------------------------------------------------------------ люди
def build_person(style):
    """Возвращает словарь групп частей тела: Body, Head, ArmPos, ArmNeg, LegPos, LegNeg (+x / -x)."""
    G={"Body":[],"Head":[],"ArmPos":[],"ArmNeg":[],"LegPos":[],"LegNeg":[]}
    C=T.CELLS
    short_sleeve = style.get("short_sleeves",True)
    dress = style.get("dress",False)
    side=lambda s_: "Pos" if s_>0 else "Neg"
    for s_ in (-1,1):
        G["Leg"+side(s_)].append(box(s_*0.09,0.04,0.045,0.11,0.08,0.29,cell=C["shoes"]))
        leg_cell = C["skin"] if dress else C["pants"]
        G["Leg"+side(s_)].append(lathe([(0.055,0.08),(0.06,0.4),(0.083,0.92)],seg=12,cell=leg_cell,center=(s_*0.09,0,0),caps=True))
    G["Body"].append(lathe([(0.15,0.88),(0.17,0.97),(0.16,1.05)],seg=16,ax=1.0,az=0.7,cell=(C["accent"] if dress else C["pants"]),caps=True))
    if dress:
        G["Body"].append(lathe([(0.19,0.50),(0.23,0.70),(0.20,0.90),(0.17,1.0)],seg=20,ax=1.1,az=0.95,cell=C["accent"],caps=False))
    G["Body"].append(lathe([(0.15,0.98),(0.165,1.12),(0.185,1.30),(0.20,1.42),(0.17,1.50)],seg=20,ax=1.0,az=0.62,cell=C["shirt"],caps=True))
    G["Body"].append(lathe([(0.052,1.48),(0.052,1.64)],seg=12,cell=C["skin"],caps=True))
    for s_ in (-1,1):
        A=G["Arm"+side(s_)]
        A.append(ellipsoid(s_*0.205,1.43,0,0.065,0.065,0.065,seg=12,rings=8,cell=C["shirt"]))
        upper = lathe([(0.048,1.12),(0.058,1.42)],seg=12,cell=C["shirt"],caps=True)
        upper = rotate(upper,'z',s_*5,pivot=(0,1.42,0)); A.append(translate(upper,(s_*0.215,0,0)))
        lowcell = C["skin"] if short_sleeve else C["shirt"]
        lower = lathe([(0.036,0.84),(0.048,1.12)],seg=12,cell=lowcell,caps=True)
        lower = rotate(lower,'z',s_*5,pivot=(0,1.42,0)); A.append(translate(lower,(s_*0.215,0,0)))
        hand = ellipsoid(0,0,0,0.04,0.055,0.03,seg=10,rings=6,cell=C["skin"])
        hand = rotate(hand,'z',s_*5,pivot=(0,0,0)); A.append(translate(hand,(s_*0.215+s_*0.07,0.80,0.0)))
    HEAD=[]
    HEAD.append(ellipsoid(0,0,0,0.095,0.12,0.105,seg=28,rings=18,cell=C["head"]))
    HEAD.append(ellipsoid(0,-0.015,0.108,0.015,0.017,0.017,seg=8,rings=6,cell=C["skin"]))
    for s_ in (-1,1): HEAD.append(ellipsoid(s_*0.096,-0.01,0.0,0.014,0.03,0.022,seg=8,rings=6,cell=C["skin"]))
    hs = style["hair_style"]
    if hs in ("long","short","pony"):
        cap = ellipsoid(0,0.015,-0.008,0.103,0.128,0.112,th0=0,th1=108 if hs!="short" else 100,seg=28,rings=14,cell=C["hair"])
        HEAD.append(rotate(cap,'x',-10))
    if hs=="long":
        back = lathe([(0.06,-0.40),(0.10,-0.20),(0.11,-0.03)],seg=16,ax=1.15,az=0.55,cell=C["hair"],caps=True)
        HEAD.append(translate(back,(0,0,-0.07)))
    if hs=="pony":
        HEAD.append(ellipsoid(0,0.07,-0.12,0.04,0.04,0.04,seg=10,rings=6,cell=C["hair"]))
        tail = lathe([(0.02,-0.25),(0.035,-0.10),(0.045,0.06)],seg=10,cell=C["hair"],caps=True)
        HEAD.append(translate(tail,(0,0,-0.14)))
    if style.get("beard"):
        HEAD.append(ellipsoid(0,-0.01,0.012,0.099,0.123,0.109,th0=112,th1=150,seg=28,rings=6,cell=C["hair"]))
    if style.get("cap"):
        dome = ellipsoid(0,0.025,0,0.108,0.115,0.116,th0=0,th1=92,seg=28,rings=10,cell=C["accent"])
        HEAD.append(rotate(dome,'x',-6))
        HEAD.append(box(0,0.07,0.13,0.17,0.012,0.09,cell=C["accent"]))
    K=1.22
    for part in HEAD:
        sc=Part([[(v[0]*K,v[1]*K,v[2]*K) for v in p] for p in part.polys], part.uvs, part.smooth)
        G["Head"].append(translate(sc,(0,1.72,0)))
    return G

PIVOTS={"Body":(0,0,0),"Head":(0,1.62,0),"ArmPos":(0.205,1.43,0),"ArmNeg":(-0.205,1.43,0),"LegPos":(0.09,0.92,0),"LegNeg":(-0.09,0.92,0)}

PEOPLE = {
 "Anna": dict(skin=(0.97,0.80,0.68), hair=(0.30,0.16,0.08), shirt=(0.93,0.93,0.95), shirt2=(0.85,0.15,0.25), pattern="stripes",
              pants=(0.2,0.2,0.3), shoes=(0.35,0.18,0.10), accent=(0.15,0.25,0.55), accent2=(1,1,1), accent_dots=True,
              eyes=(0.2,0.55,0.35), brow=(0.25,0.13,0.07), lips=(0.8,0.2,0.3), blush=(0.98,0.65,0.62),
              style=dict(hair_style="long", dress=True, short_sleeves=True)),
 "Boris":dict(skin=(0.86,0.66,0.50), hair=(0.08,0.07,0.07), shirt=(0.15,0.45,0.25), shirt2=(0.05,0.12,0.08), pattern="plaid",
              pants=(0.18,0.28,0.50), shoes=(0.92,0.92,0.92), accent=(0.15,0.25,0.65), accent2=(1,1,1), accent_dots=False,
              eyes=(0.35,0.22,0.1), brow=(0.08,0.07,0.07), lips=(0.65,0.3,0.28), blush=(0.88,0.58,0.45),
              style=dict(hair_style="short", beard=True, cap=True, short_sleeves=False)),
 "Vera": dict(skin=(0.92,0.74,0.60), hair=(0.88,0.72,0.30), shirt=(0.30,0.55,0.85), shirt2=(1,1,1), pattern="polo",
              pants=(0.38,0.38,0.42), shoes=(0.95,0.95,0.95), accent=(0.9,0.75,0.2), accent2=(1,1,1), accent_dots=False,
              eyes=(0.25,0.45,0.75), brow=(0.55,0.4,0.15), lips=(0.85,0.35,0.4), blush=(0.98,0.68,0.60),
              style=dict(hair_style="pony", short_sleeves=True)),
}
for name,p in PEOPLE.items():
    G=build_person(p["style"])
    allparts=[pt for k in G for pt in G[k]]
    write_obj(mp("Person_"+name),"Person_"+name,allparts)                      # для просмотра целиком
    write_parts(os.path.join(MODELS,"Person_"+name+".parts"),[(k,PIVOTS[k],G[k]) for k in ("Body","Head","ArmPos","ArmNeg","LegPos","LegNeg")])
    T.person_atlas(tp("Tex_Person_"+name),p)

# ------------------------------------------------------------------ каталог декоративных (теперь рабочих) товаров
with open(os.path.join(TEX,"decor_catalog.txt"),"w",encoding="utf-8") as f:
    for i,(m,label,bg,band,fg) in enumerate(DECOR):
        f.write("Decor_%s_%02d|%s\n"%(m,i,label))

# ------------------------------------------------------------------ природа вокруг магазина
NAT={"bark":(0.0,0.0,0.5,1.0),"leaf":(0.5,0.0,1.0,1.0)}
def nature_tex():
    img=Image.new("RGB",(256,128))
    T.fill_noise(img,(0,0,128,128),(0.33,0.22,0.14),amp=16,scale=2)
    T.fill_noise(img,(128,0,256,128),(0.20,0.50,0.18),amp=22,scale=3)
    img.save(tp("Tex_Nature"))
nature_tex()
tree=[lathe([(0.20,0.0),(0.15,1.2),(0.11,2.4)],seg=10,cell=NAT["bark"],caps=True),
      ellipsoid(0,3.3,0,1.45,1.25,1.45,seg=14,rings=9,cell=NAT["leaf"]),
      ellipsoid(0.85,2.8,0.35,0.95,0.85,0.95,seg=12,rings=8,cell=NAT["leaf"]),
      ellipsoid(-0.75,3.0,-0.45,1.05,0.95,1.05,seg=12,rings=8,cell=NAT["leaf"])]
write_obj(mp("Tree"),"Tree",tree)
pine=[lathe([(0.16,0.0),(0.11,1.4)],seg=8,cell=NAT["bark"],caps=True)]
for y0,r in ((1.0,1.25),(2.0,1.0),(3.0,0.75)):
    pine.append(lathe([(r,y0),(0.0,y0+1.7)],seg=12,cell=NAT["leaf"],caps=True,smooth=True))
write_obj(mp("Pine"),"Pine",pine)
bush=[]
for dx,dy,dz,r in ((0,0.45,0,0.62),(0.5,0.38,0.2,0.48),(-0.5,0.36,-0.1,0.5),(0.1,0.35,-0.5,0.45),(-0.15,0.3,0.5,0.42)):
    bush.append(ellipsoid(dx,dy,dz,r,r*0.85,r,seg=12,rings=8,cell=NAT["leaf"]))
write_obj(mp("Bush"),"Bush",bush)

print("моделей:",len([f for f in os.listdir(MODELS) if f.endswith('.obj')]),"текстур:",len(os.listdir(TEX)))
