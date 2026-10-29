# Превью: NPC в позе ходьбы с повёрнутой головой и деревья/кусты
import os, numpy as np
import meshlib as ML
import hand_skin as H
import render_preview as RP

ROOT=H.ROOT; MODELS=os.path.join(ROOT,"Assets","Models"); TEX=os.path.join(ROOT,"Assets","Textures")

def posed(name, leg=28, arm=22, head_yaw=0, head_pitch=0):
    parts=ML.read_parts(os.path.join(MODELS,"Person_%s.parts"%name))
    Vs=[];Ns=[];Us=[];Ts=[];off=0
    for pname,pivot,V,N,U,T in parts:
        M=np.eye(4)
        if pname=="LegPos": M=H.Rx(leg)
        elif pname=="LegNeg": M=H.Rx(-leg)
        elif pname=="ArmPos": M=H.Rx(-arm)
        elif pname=="ArmNeg": M=H.Rx(arm)
        elif pname=="Head": M=H.Ry(head_yaw)@H.Rx(head_pitch)
        R=M[:3,:3]
        Vw=V@R.T+pivot; Nw=N@R.T
        Vs.append(Vw);Ns.append(Nw);Us.append(U);Ts.append(T+off);off+=len(V)
    return np.vstack(Vs),np.vstack(Us),np.vstack(Ns),np.vstack(Ts)

imgs=[];titles=[]
for name in ("Anna","Boris","Vera"):
    tex=os.path.join(TEX,"Tex_Person_%s.png"%name)
    imgs.append(RP.render(posed(name,28,22,0,0),tex,yaw=-70,pitch=4,size=(420,560))); titles.append("%s — шаг (вид сбоку)"%name)
    imgs.append(RP.render(posed(name,-20,-15,40,-8),tex,yaw=-25,pitch=4,size=(420,560))); titles.append("%s — шаг + голова к игроку"%name)
RP.sheet(imgs,2,titles,cell=(420,560),title="Анимация NPC: ноги и руки качаются, голова поворачивается к игроку").save(os.path.join(ROOT,"NPC_Walk_Preview.png"))

tex=os.path.join(TEX,"Tex_Nature.png")
imgs=[RP.render(os.path.join(MODELS,m+".obj"),tex,yaw=-30,pitch=12,size=(420,420)) for m in ("Tree","Pine","Bush")]
RP.sheet(imgs,3,["Дерево","Ель","Куст"],cell=(420,420),title="Деревья и кусты вокруг здания").save(os.path.join(ROOT,"Nature_Preview.png"))
print("ok")
