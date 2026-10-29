# Рисует превью моделей с текстурами (простой растеризатор на numpy)
import os, math, glob
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import texlib as T

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MODELS = os.path.join(ROOT, "Assets", "Models")
TEX = os.path.join(ROOT, "Assets", "Textures")

def load_obj(path):
    V=[];VT=[];VN=[];tris=[]
    for line in open(path,encoding="utf-8"):
        s=line.split()
        if not s: continue
        if s[0]=="v": V.append([float(x) for x in s[1:4]])
        elif s[0]=="vt": VT.append([float(x) for x in s[1:3]])
        elif s[0]=="vn": VN.append([float(x) for x in s[1:4]])
        elif s[0]=="f":
            idx=[int(t.split("/")[0])-1 for t in s[1:]]
            for k in range(1,len(idx)-1): tris.append((idx[0],idx[k],idx[k+1]))
    return np.array(V),np.array(VT),np.array(VN),np.array(tris)

def render(obj, texpath, yaw=0, pitch=12, size=(420,640), bg=(236,238,242), fit=None, tint=None):
    V,VT,VN,tris=(obj if isinstance(obj,tuple) else load_obj(obj))
    tex=np.asarray(Image.open(texpath).convert("RGB")).astype(np.float32)/255.0 if texpath else None
    base=np.array(tint if tint else (0.7,0.7,0.75),np.float32)
    a=math.radians(yaw); b=math.radians(pitch)
    Ry=np.array([[math.cos(a),0,math.sin(a)],[0,1,0],[-math.sin(a),0,math.cos(a)]])
    Rx=np.array([[1,0,0],[0,math.cos(b),-math.sin(b)],[0,math.sin(b),math.cos(b)]])
    R=Rx@Ry
    c=(V.min(0)+V.max(0))/2
    P=(V-c)@R.T; N=VN@R.T
    ext=(P.max(0)-P.min(0))
    W,H=size
    s=min(W*0.86/max(ext[0],1e-6),H*0.9/max(ext[1],1e-6)) if fit is None else fit
    X=W/2+P[:,0]*s; Y=H/2-P[:,1]*s; Z=P[:,2]
    img=np.zeros((H,W,3),np.float32); img[:]=np.array(bg)/255.0
    zb=np.full((H,W),-1e9,np.float32)
    L=np.array([-0.4,0.6,0.7]); L/=np.linalg.norm(L)
    order=np.argsort([-Z[t].mean() for t in tris])
    for ti in order:
        i0,i1,i2=tris[ti]
        x=np.array([X[i0],X[i1],X[i2]]); y=np.array([Y[i0],Y[i1],Y[i2]])
        area=(x[1]-x[0])*(y[2]-y[0])-(x[2]-x[0])*(y[1]-y[0])
        if abs(area)<1e-6: continue
        # лицевая сторона: нормаль смотрит на камеру (+Z после поворота)
        fn=np.cross(P[i1]-P[i0],P[i2]-P[i0])
        if fn[2] <= 0: continue
        minx=max(int(x.min()),0); maxx=min(int(x.max())+1,W-1); miny=max(int(y.min()),0); maxy=min(int(y.max())+1,H-1)
        if minx>maxx or miny>maxy: continue
        gx,gy=np.meshgrid(np.arange(minx,maxx+1)+0.5,np.arange(miny,maxy+1)+0.5)
        w0=((x[1]-gx)*(y[2]-gy)-(x[2]-gx)*(y[1]-gy))/area
        w1=((x[2]-gx)*(y[0]-gy)-(x[0]-gx)*(y[2]-gy))/area
        w2=1-w0-w1
        m=(w0>=-1e-4)&(w1>=-1e-4)&(w2>=-1e-4)
        if not m.any(): continue
        z=w0*Z[i0]+w1*Z[i1]+w2*Z[i2]
        sub_zb=zb[miny:maxy+1,minx:maxx+1]
        m&=z>sub_zb
        if not m.any(): continue
        n=w0[...,None]*N[i0]+w1[...,None]*N[i1]+w2[...,None]*N[i2]
        n/=np.linalg.norm(n,axis=2,keepdims=True)+1e-9
        lam=np.clip((n*L).sum(2),0,1)*0.65+0.42
        if tex is not None and len(VT):
            u=w0*VT[i0,0]+w1*VT[i1,0]+w2*VT[i2,0]; v=w0*VT[i0,1]+w1*VT[i1,1]+w2*VT[i2,1]
            tx=np.clip((u%1.0001)*(tex.shape[1]-1),0,tex.shape[1]-1).astype(int)
            ty=np.clip((1-(v%1.0001))*(tex.shape[0]-1),0,tex.shape[0]-1).astype(int)
            col=tex[ty,tx]
        else:
            col=np.broadcast_to(base,(maxy-miny+1,maxx-minx+1,3))
        col=np.clip(col*lam[...,None],0,1)
        reg=img[miny:maxy+1,minx:maxx+1]; reg[m]=col[m]
        sub_zb[m]=z[m]
    return Image.fromarray((img*255).astype(np.uint8))

def label(im,text,y=6,size=22):
    d=ImageDraw.Draw(im); f=T.font(size)
    bb=d.textbbox((0,0),text,font=f); d.text(((im.width-(bb[2]-bb[0]))/2,y),text,font=f,fill=(40,40,50))

def sheet(images, cols, titles, cell=(420,640), pad=10, title="") :
    rows=(len(images)+cols-1)//cols
    top=50 if title else 0
    W=cols*cell[0]+(cols+1)*pad; H=rows*(cell[1]+40)+(rows+1)*pad+top
    out=Image.new("RGB",(W,H),(236,238,242)); d=ImageDraw.Draw(out)
    if title: d.text((pad+10,10),title,font=T.font(30),fill=(30,30,40))
    for i,(im,t) in enumerate(zip(images,titles)):
        r,c=divmod(i,cols)
        x=pad+c*(cell[0]+pad); y=top+pad+r*(cell[1]+40+pad)
        out.paste(im,(x,y+36)); 
        d.text((x+8,y+4),t,font=T.font(22),fill=(40,40,50))
    return out

def main():
    # --- люди
    imgs=[];ts=[]
    for name in ("Anna","Boris","Vera"):
        for yaw,lab in ((0,"спереди"),(-35,"3/4"),(180,"сзади")):
            imgs.append(render(os.path.join(MODELS,"Person_%s.obj"%name),os.path.join(TEX,"Tex_Person_%s.png"%name),yaw=yaw,pitch=6))
            ts.append("%s — %s"%(name,lab))
    sheet(imgs,3,ts,title="Покупатели (NPC) — модель + текстура").save(os.path.join(ROOT,"NPC_Preview.png"))
    # --- товары
    items=[("Milk","Tex_Milk"),("Bread","Tex_Bread"),("Cola","Tex_Cola"),("Apple","Tex_Apple"),("Soap","Tex_Soap"),("Cereal","Tex_Cereal"),("Banknote","Tex_Banknote")]
    imgs=[];ts=[]
    for m,t in items:
        imgs.append(render(os.path.join(MODELS,m+".obj"),os.path.join(TEX,t+".png"),yaw=-30,pitch=22,size=(300,300),bg=(236,238,242)));ts.append(m+" (можно брать)")
    for t in sorted(glob.glob(os.path.join(TEX,"Decor_*.png"))):
        base=os.path.basename(t)[:-4]; m=base.split("_")[1]
        imgs.append(render(os.path.join(MODELS,m+".obj"),t,yaw=-30,pitch=22,size=(300,300))); ts.append(base.replace("Decor_","")+" (декор)")
    sheet(imgs,6,ts,cell=(300,300),title="Продукты: интерактивные и декоративные (с этикетками)").save(os.path.join(ROOT,"Products_Preview.png"))
    # --- интерьер (однотонные)
    inter=[("Shelf",(0.75,0.75,0.8)),("Basket",(0.9,0.3,0.1)),("Counter",(0.55,0.38,0.22)),("Register",(0.25,0.27,0.3)),("Stairs",(0.6,0.6,0.62)),("Plant",(0.3,0.6,0.3))]
    imgs=[];ts=[]
    for m,c in inter:
        imgs.append(render(os.path.join(MODELS,m+".obj"),None,yaw=-35,pitch=22,size=(420,420),tint=c)); ts.append(m)
    sheet(imgs,3,ts,cell=(420,420),title="Интерьер").save(os.path.join(ROOT,"Interior_Preview.png"))
    print("ok")
if __name__=="__main__":
    main()
