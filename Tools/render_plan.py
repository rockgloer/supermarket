# Рисует план магазина (вид сверху) по тем же координатам, что в SupermarketBuilder.cs
import os
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle, FancyArrowPatch
import matplotlib.font_manager as fm
import texlib as T

fm.fontManager.addfont(T.FONT_B)
plt.rcParams["font.family"]="DejaVu Sans"

ROOT=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..")

BUSHES=[(x,-7.9) for x in (-7.6,-5.6,-3.6,-1.6,0.4,7.8)]+[(9.9,z) for z in (-4,-1.5,1,3.5)]+[(-9.9,z) for z in (-4,-1.5,1,3.5)]+[(x,7.9) for x in (-7,-3.5,0,3.5,7)]
TREES=[(-12.5,-8.5),(12.5,-8),(12,3),(-12.5,2.5),(-8,11.5),(8.5,11)]
PINES=[(-12,9),(12.5,8.5),(0,12.5),(-12.5,-3.5)]
def nature(ax):
    ax.add_patch(Rectangle((-13,-10.5),26,4.2,fc="#c9c9c4",ec="none",zorder=0.5))
    for x,z in BUSHES: ax.add_patch(Circle((x,z),0.55,fc="#4f9a47",ec="#2d6a2b",zorder=3))
    for x,z in TREES: ax.add_patch(Circle((x,z),1.4,fc="#3f8a3c",ec="#2d6a2b",alpha=0.85,zorder=3))
    for x,z in PINES: ax.add_patch(Circle((x,z),1.1,fc="#2e6b3a",ec="#1d4a28",alpha=0.9,zorder=3))

def base(ax,title):
    ax.set_xlim(-14,14); ax.set_ylim(-11.5,14); ax.set_aspect("equal")
    ax.add_patch(Rectangle((-8.3,-6.3),16.6,12.6,fill=False,lw=3,ec="#444"))
    ax.set_title(title,fontsize=15,weight="bold")
    ax.set_xticks([]); ax.set_yticks([])
    ax.add_patch(Rectangle((-14,-11.5),28,25.5,color="#dfe9d8",zorder=0))
    nature(ax)
    ax.text(0,-11.0,"дорога",ha="center",color="#555",fontsize=10)

def shelf(ax,x,z,color="#b8bcc8",label=None):
    ax.add_patch(Rectangle((x-1,z-0.25),2,0.5,fc=color,ec="#555",lw=1))
    if label: ax.text(x,z,label,ha="center",va="center",fontsize=7,weight="bold")

def window(ax,side,a,b):
    if side=="S": ax.plot([a,b],[-6.15,-6.15],color="#4aa3df",lw=6,solid_capstyle="butt")
    if side=="N": ax.plot([a,b],[6.15,6.15],color="#4aa3df",lw=6,solid_capstyle="butt")
    if side=="W": ax.plot([-8.15,-8.15],[a,b],color="#4aa3df",lw=6,solid_capstyle="butt")
    if side=="E": ax.plot([8.15,8.15],[a,b],color="#4aa3df",lw=6,solid_capstyle="butt")

fig,axs=plt.subplots(1,2,figsize=(20,11),dpi=100)

# ---- этаж 1
ax=axs[0]; base(ax,"ЭТАЖ 1")
for a,b in ((-1.8,1.0),(6.0,7.4)): window(ax,"S",a,b)
ax.plot([2.8,5.2],[-6.15,-6.15],color="#e8a33d",lw=8,solid_capstyle="butt"); ax.text(4,-8.0,"ВХОД",ha="center",weight="bold",color="#b36b00")
for a,b in ((-6.5,-3.5),(-1.5,1.5),(3.5,6.5)): window(ax,"N",a,b)
window(ax,"W",1.6,4.6); window(ax,"E",1.2,4.8)
# лестница
ax.add_patch(Rectangle((5.5,-5.9),2,5.6,fc="#cfcfd6",ec="#555"))
for i in range(1,20): ax.plot([5.5,7.5],[-5.9+i*0.28]*2,color="#999",lw=0.6)
ax.annotate("",xy=(6.5,-0.6),xytext=(6.5,-5.5),arrowprops=dict(arrowstyle="->",color="#444",lw=2)); ax.text(6.5,-3.2,"вверх",rotation=90,ha="center",va="center",weight="bold")
# стеллажи
A=[(-6.4,"МОЛОКО"),(-4.2,"ХЛЕБ"),(-2.0,None),(0.2,None),(2.6,None)]
B=[(-6.4,"НАПИТКИ"),(-4.2,"ФРУКТЫ"),(-2.0,None),(0.2,None),(2.6,None)]
for x,l in A: shelf(ax,x,0.8,"#f3c36b" if l else "#c9ccd6",l)
for x,l in B: shelf(ax,x,3.2,"#f3c36b" if l else "#c9ccd6",l)
for x in (-6.4,-4.2,-2.0,0.2,2.4,4.4): shelf(ax,x,5.55)
# касса
ax.add_patch(Rectangle((-5.7,-3.4),2.4,0.8,fc="#a9744a",ec="#333")); ax.text(-4.5,-3.0,"КАССА",ha="center",va="center",color="white",weight="bold",fontsize=9)
ax.add_patch(Rectangle((-5.5,-3.2),0.4,0.4,fc="#333"))
ax.add_patch(Circle((-4.5,-2.0),0.25,fc="#e55",ec="#333")); ax.text(-4.5,-1.45,"вы (кассир)",ha="center",fontsize=9,weight="bold",color="#a22")
ax.add_patch(Rectangle((-2.85,-2.4),0.5,0.34,fc="#e8671a",ec="#333")); ax.text(-2.6,-2.9,"корзина",ha="center",fontsize=8)
# покупатель: маршрут
path=[(4,-9.5),(4,-4.6),(-4.5,-4.6),(-4.5,-4.2)]
ax.plot([p[0] for p in path],[p[1] for p in path],"--",color="#2a7ad4",lw=2.5)
ax.add_patch(Circle((-4.5,-4.2),0.25,fc="#2a7ad4",ec="#123")); ax.text(-4.5,-4.9,"покупатель",ha="center",fontsize=9,color="#2a7ad4",weight="bold")
ax.text(-0.2,-4.1,"путь покупателя",color="#2a7ad4",fontsize=9,ha="center")
# декор
ax.add_patch(Rectangle((1.6,-5.7),0.6,0.6,fc="#e8671a")); ax.text(1.9,-6.35,"корзины",ha="center",fontsize=7)
for x,z in ((1.0,-5.5),(-7.3,-5.4)): ax.add_patch(Circle((x,z),0.3,fc="#58a85a",ec="#333"))
ax.text(-7.9,-1.2,"доска\nуправления",fontsize=7,rotation=90,va="center",ha="center",color="#235")
ax.text(-4.5,-5.75,"доска «ЗАДАНИЕ»",ha="center",fontsize=8,color="#235")
ax.add_patch(Rectangle((4.95,-4.45),0.12,2.9,fc="#2b2f36",ec="#000")); ax.text(4.4,-3.0,"указатель\nтоваров",rotation=90,ha="center",va="center",fontsize=8,color="#235")

# ---- этаж 2
ax=axs[1]; base(ax,"ЭТАЖ 2")
for a,b in ((-6.8,-3.0),(-1.8,1.0),(2.8,5.2),(6.0,7.4)): window(ax,"S",a,b)
for a,b in ((-6.5,-3.5),(-1.5,1.5),(3.5,6.5)): window(ax,"N",a,b)
window(ax,"W",-4.5,-1.0); window(ax,"W",1.6,4.6); window(ax,"E",-5.0,-1.2); window(ax,"E",1.2,4.8)
ax.add_patch(Rectangle((5.5,-6),2,5.7,fc="#222",alpha=0.12,ec="#aa0",ls="--")); ax.text(6.5,-3.2,"проём\nлестницы",ha="center",va="center",fontsize=9)
ax.plot([5.5,5.5],[-5.9,-0.3],color="#333",lw=4); ax.text(5.15,-3.2,"перила",rotation=90,va="center",ha="center",fontsize=8)
for x,l in ((-6.4,"МЫЛО"),(-4.2,"ХЛОПЬЯ")): shelf(ax,x,0.8,"#f3c36b",l)
for x in (-2.0,0.2,2.6): shelf(ax,x,0.8)
for x in (-6.4,-4.2,-2.0,0.2,2.6): shelf(ax,x,3.2)
for x in (-6.4,-4.2,-2.0,0.2,2.4,4.4): shelf(ax,x,5.55)
ax.add_patch(Circle((7.3,5.3),0.3,fc="#58a85a",ec="#333"))
ax.add_patch(Rectangle((7.9,-1.15),0.12,2.3,fc="#2b2f36",ec="#000")); ax.text(7.4,0,"указатель\nтоваров",rotation=90,ha="center",va="center",fontsize=7,color="#235")
ax.text(0,-3.5,"свободная площадка у лестницы и окон",ha="center",fontsize=10,color="#777")
fig.suptitle("План супермаркета (вид сверху, окна — голубые полосы)",fontsize=18,weight="bold")
plt.tight_layout()
plt.savefig(os.path.join(ROOT,"Store_Plan.png"))
print("ok")
