# Превью цельной руки в разных позах (линейный скиннинг, как в Unity)
import os, math
import numpy as np
import hand_skin as H
import render_preview as RP

ROOT=H.ROOT
d=np.load(os.path.join(ROOT,"Tools","_hand_cache.npz"))
verts,nrm,idx,bi,bw=d["verts"],d["nrm"],d["idx"],d["bi"],d["bw"]
bones=H.build_rig()
Wrest=H.world_matrices(bones)

def pose(trigger,grip,thumb):
    curl={}
    for f in range(4):
        t=trigger if f==0 else grip
        for j in range(3): curl["f%dj%d"%(f,j+1)]=H.Ry(-t*H.FINGER_MAX[j])
    rest=bones[13][3]
    curl["t1"]=H.Rz(thumb*H.THUMB_MAX[0])
    curl["t2"]=np.linalg.inv(rest)@H.Rz(thumb*H.THUMB_MAX[1])@rest
    return curl

def skin(curl):
    W=H.world_matrices(bones,curl)
    S=[W[i]@np.linalg.inv(Wrest[i]) for i in range(len(bones))]
    Vh=np.c_[verts,np.ones(len(verts))]
    out=np.zeros((len(verts),3)); no=np.zeros((len(verts),3))
    for k in range(4):
        for b in range(len(bones)):
            m=(bi[:,k]==b)&(bw[:,k]>0)
            if not m.any(): continue
            out[m]+=bw[m,k,None]*(Vh[m]@S[b].T)[:,:3]
            no[m]+=bw[m,k,None]*(nrm[m]@S[b][:3,:3].T)
    return out,no

tris=np.concatenate([idx[:,[0,1,2]]],0)
imgs=[];titles=[]
for title,tr,gr,th in (("Открытая",0,0,0),("Кулак",1,1,1),("Указание пальцем",0,1,1),("Только курок",1,0,0)):
    V,N=skin(pose(tr,gr,th))
    mesh=(V,np.zeros((len(V),2)),N,tris)
    for yaw,lab in ((90,"ладонь внутрь"),(-40,"3/4")):
        imgs.append(RP.render(mesh,None,yaw=yaw,pitch=10,size=(420,420),tint=(0.55,0.55,0.6)))
        titles.append("%s — %s"%(title,lab))
RP.sheet(imgs,4,titles,cell=(420,420),title="Руки (правая): цельная сетка на скелете (серый цвет — только для превью)").save(os.path.join(ROOT,"Hands_Preview.png"))
print("ok")
