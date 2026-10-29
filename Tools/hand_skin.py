# Цельная гладкая рука (одна сетка + веса костей). Строится из SDF + marching tetrahedra.
# Выход: Assets/Models/HandSkin.obj (для просмотра) и HandSkin.bytes (для сборки сцены в Unity).
import os, math, struct
import numpy as np

ROOT=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..")
MODELS=os.path.join(ROOT,"Assets","Models")

# ---------- скелет (правая рука: пальцы +Z, большой палец +Y, ладонь смотрит в -X) ----------
SEG=[0.040,0.026,0.020]
FINGER_BASE=[(0.0,0.034,0.046),(0.0,0.0115,0.050),(0.0,-0.011,0.048),(0.0,-0.031,0.042)]
FINGER_SCALE=[1.0,1.08,1.0,0.82]
FINGER_SPREAD=[-5.0,-1.5,2.5,7.0]            # веер пальцев, градусы вокруг оси X
THUMB_BASE=(-0.014,0.030,0.012)
THUMB_DIR=(-0.15,0.75,0.65)
THUMB_LEN=[0.038,0.028]; THUMB_TIP=0.024
FINGER_MAX=[75.0,95.0,65.0]; THUMB_MAX=[55.0,60.0]

def Rx(a):
    a=math.radians(a); c,s=math.cos(a),math.sin(a); return np.array([[1,0,0,0],[0,c,-s,0],[0,s,c,0],[0,0,0,1]],float)
def Ry(a):
    a=math.radians(a); c,s=math.cos(a),math.sin(a); return np.array([[c,0,s,0],[0,1,0,0],[-s,0,c,0],[0,0,0,1]],float)
def Rz(a):
    a=math.radians(a); c,s=math.cos(a),math.sin(a); return np.array([[c,-s,0,0],[s,c,0,0],[0,0,1,0],[0,0,0,1]],float)
def Tr(x,y,z):
    m=np.eye(4); m[:3,3]=[x,y,z]; return m
def from_to(a,b):
    a=np.array(a,float); b=np.array(b,float); a/=np.linalg.norm(a); b/=np.linalg.norm(b)
    v=np.cross(a,b); c=a.dot(b); m=np.eye(4)
    if np.linalg.norm(v)<1e-9: return m
    vx=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]])
    m[:3,:3]=np.eye(3)+vx+vx@vx*(1/(1+c)); return m

def build_rig():
    """Список костей: (имя, родитель, локальная позиция, поворот покоя 4x4, длина кончика или None)"""
    bones=[("root",-1,np.zeros(3),np.eye(4),None)]
    for f in range(4):
        s=FINGER_SCALE[f]; b=FINGER_BASE[f]
        i0=len(bones)
        bones.append(("f%dj1"%f,0,np.array(b),Rx(FINGER_SPREAD[f]),None))
        bones.append(("f%dj2"%f,i0,np.array([0,0,SEG[0]*s]),np.eye(4),None))
        bones.append(("f%dj3"%f,i0+1,np.array([0,0,SEG[1]*s]),np.eye(4),SEG[2]*s))
    rest=from_to((0,0,1),THUMB_DIR)
    t0=len(bones)
    bones.append(("t1",0,np.array(THUMB_BASE),rest,None))
    bones.append(("t2",t0,np.array([0,0,THUMB_LEN[0]]),np.eye(4),THUMB_TIP))
    return bones

def world_matrices(bones, curl=None):
    """curl: словарь имя->4x4 дополнительный локальный поворот"""
    W=[]
    for i,(name,par,pos,rest,tip) in enumerate(bones):
        L=Tr(*pos)@rest
        if curl and name in curl: L=L@curl[name]
        W.append(L if par<0 else W[par]@L)
    return W

# ---------- SDF ----------
def seg_dist(P,a,b):
    ab=b-a; t=np.clip(((P-a)@ab)/ab.dot(ab),0,1); c=a+t[:,None]*ab
    return np.linalg.norm(P-c,axis=1),t
def capsule(P,a,b,ra,rb):
    d,t=seg_dist(P,a,b); return d-(ra+(rb-ra)*t)
def smin(a,b,k):
    h=np.clip(0.5+0.5*(b-a)/k,0,1); return b*(1-h)+a*h-k*h*(1-h)
def ell(P,c,r):
    q=(P-c)/r; k0=np.linalg.norm(q,axis=1); k1=np.linalg.norm(q/r,axis=1); return k0*(k0-1)/np.maximum(k1,1e-9)
def rbox(P,c,half,r):
    q=np.abs(P-c)-half; return np.linalg.norm(np.maximum(q,0),axis=1)+np.minimum(q.max(axis=1),0)-r

def make_sdf(bones):
    W=world_matrices(bones)
    pos=[w[:3,3] for w in W]
    zdir=[w[:3,2] for w in W]
    caps=[]
    for f in range(4):
        s=FINGER_SCALE[f]; k=0.9+0.1*s
        i=1+3*f
        p0,p1,p2=pos[i],pos[i+1],pos[i+2]; p3=p2+zdir[i+2]*SEG[2]*s
        caps.append((p0,p1,0.0092*k,0.0084*k)); caps.append((p1,p2,0.0084*k,0.0076*k)); caps.append((p2,p3,0.0076*k,0.0064*k))
    t1,t2=pos[13],pos[14]; t3=t2+zdir[14]*THUMB_TIP
    caps.append((t1,t2,0.0120,0.0102)); caps.append((t2,t3,0.0102,0.0082))
    def sdf(P):
        # ладонь: «оболочка» из капсул от запястья к основаниям пальцев — сужается к запястью
        wr=np.array([0.0,0.0,-0.040])
        d=capsule(P,np.array([-0.001,0.034,0.044]),np.array([-0.001,-0.031,0.040]),0.0115,0.0115)       # линия костяшек
        for f in range(4):
            b=np.array(FINGER_BASE[f]); b=b+np.array([0.0,0.0,-0.004])
            d=smin(d,capsule(P,b,wr+np.array([0.0,0.0,0.0]),0.0115,0.016),0.012)
        d=smin(d,ell(P,np.array([0.003,0.0,0.004]),np.array([0.010,0.030,0.036])),0.010)                  # тыл ладони
        d=smin(d,ell(P,np.array([-0.011,0.020,0.014]),np.array([0.012,0.020,0.026])),0.012)               # бугор большого пальца
        # запястье — овал (две капсулы рядом)
        for yy in (-0.008,0.008):
            d=smin(d,capsule(P,np.array([0,yy,-0.035]),np.array([0,yy,-0.105]),0.0175,0.0165),0.012)
        for a_,b_,ra,rb in caps:
            d=smin(d,capsule(P,a_,b_,ra,rb),0.0045)
        return d
    return sdf

# ---------- marching tetrahedra ----------
def mesh_from_sdf(sdf, lo, hi, h):
    xs=np.arange(lo[0],hi[0]+h,h); ys=np.arange(lo[1],hi[1]+h,h); zs=np.arange(lo[2],hi[2]+h,h)
    nx,ny,nz=len(xs),len(ys),len(zs)
    G=np.stack(np.meshgrid(xs,ys,zs,indexing="ij"),-1).reshape(-1,3)
    V=sdf(G).reshape(nx,ny,nz)
    corners=[(0,0,0),(1,0,0),(1,1,0),(0,1,0),(0,0,1),(1,0,1),(1,1,1),(0,1,1)]
    cv=np.stack([V[c[0]:nx-1+c[0],c[1]:ny-1+c[1],c[2]:nz-1+c[2]] for c in corners],-1)
    inside=cv<0
    cross=inside.any(-1)&(~inside.all(-1))
    ci=np.argwhere(cross)
    vals=cv[cross]                                   # (C,8)
    cpos=np.stack([np.stack([xs[ci[:,0]+c[0]],ys[ci[:,1]+c[1]],zs[ci[:,2]+c[2]]],-1) for c in corners],1)  # (C,8,3)
    tets=[(0,5,1,6),(0,1,2,6),(0,2,3,6),(0,3,7,6),(0,7,4,6),(0,4,5,6)]
    tris=[]
    for t in tets:
        tv=vals[:,list(t)]; tp=cpos[:,list(t),:]
        code=((tv<0)*np.array([1,2,4,8])).sum(1)
        def interp(m,a,b):
            va=tv[m,a]; vb=tv[m,b]; u=(va/(va-vb))[:,None]; return tp[m,a]+(tp[m,b]-tp[m,a])*u
        for c in range(1,15):
            m=code==c
            if not m.any(): continue
            ins=[i for i in range(4) if c>>i&1]; out=[i for i in range(4) if not c>>i&1]
            if len(ins)==1 or len(ins)==3:
                o=ins[0] if len(ins)==1 else out[0]
                others=[i for i in range(4) if i!=o]
                tris.append(np.stack([interp(m,o,others[0]),interp(m,o,others[1]),interp(m,o,others[2])],1))
            else:
                a,b=ins; cc,d=out
                p_ac=interp(m,a,cc); p_ad=interp(m,a,d); p_bd=interp(m,b,d); p_bc=interp(m,b,cc)
                tris.append(np.stack([p_ac,p_ad,p_bd],1)); tris.append(np.stack([p_ac,p_bd,p_bc],1))
    T=np.concatenate(tris,0)                          # (N,3,3)
    flat=T.reshape(-1,3)
    key=np.round(flat/1e-6).astype(np.int64)
    uniq,inv=np.unique(key,axis=0,return_inverse=True)
    verts=np.zeros((len(uniq),3)); verts[inv.reshape(-1)]=flat
    idx=inv.reshape(-1,3)
    # выкидываем вырожденные
    a,b,c=verts[idx[:,0]],verts[idx[:,1]],verts[idx[:,2]]
    area=np.linalg.norm(np.cross(b-a,c-a),axis=1)
    idx=idx[area>1e-10]
    return verts,idx

def gradient(sdf,P,e=0.0006):
    g=np.zeros_like(P)
    for k in range(3):
        d=np.zeros(3); d[k]=e
        g[:,k]=sdf(P+d)-sdf(P-d)
    n=np.linalg.norm(g,axis=1,keepdims=True); return g/np.maximum(n,1e-12)

# ---------- веса костей ----------
def skin_weights(verts,bones,sigma=0.0065):
    W=world_matrices(bones)
    segs=[]
    # корень: ось ладони
    for i,(name,par,pos,rest,tip) in enumerate(bones):
        a=W[i][:3,3]
        if i==0:
            segs.append((np.array([0,0,-0.12]),np.array([0,0,0.045]),0.030)); continue
        kids=[j for j,b in enumerate(bones) if b[1]==i]
        if kids: bpos=W[kids[0]][:3,3]
        else: bpos=a+W[i][:3,2]*tip
        segs.append((a,bpos,0.0))
    D=np.zeros((len(verts),len(bones)))
    for i,(a,b,bias) in enumerate(segs):
        d,_=seg_dist(verts,a,b); D[:,i]=np.maximum(d-bias,0)
    Wt=np.exp(-(D/sigma)**2)
    # у корня совсем без «хвоста»: всё, что дальше от пальцев, но близко к ладони, принадлежит корню
    order=np.argsort(-Wt,axis=1)[:,:4]
    out_i=np.zeros((len(verts),4),np.int32); out_w=np.zeros((len(verts),4),np.float32)
    for n in range(len(verts)):
        ids=order[n]; w=Wt[n,ids]
        if w.sum()<1e-6:
            j=int(np.argmin(D[n])); ids=np.array([j,0,0,0]); w=np.array([1,0,0,0],float)
        w=w/w.sum(); out_i[n]=ids; out_w[n]=w
    return out_i,out_w

def main():
    bones=build_rig()
    sdf=make_sdf(bones)
    verts,idx=mesh_from_sdf(sdf,(-0.045,-0.055,-0.115),(0.045,0.115,0.150),0.0032)
    nrm=gradient(sdf,verts)
    # ориентация треугольников по градиенту SDF (наружу)
    a,b,c=verts[idx[:,0]],verts[idx[:,1]],verts[idx[:,2]]
    fn=np.cross(b-a,c-a); cen=(a+b+c)/3
    g=gradient(sdf,cen)
    flip=(fn*g).sum(1)<0
    idx[flip]=idx[flip][:,[0,2,1]]
    bi,bw=skin_weights(verts,bones)
    # OBJ для просмотра (в системе Unity-руки: +X справа)
    lines=["# HandSkin (правая рука) - создано hand_skin.py","o HandSkin"]
    for v,n in zip(verts,nrm):
        lines.append("v %.5f %.5f %.5f"%tuple(v))
    for v,n in zip(verts,nrm):
        lines.append("vn %.4f %.4f %.4f"%tuple(n))
    lines.append("vt 0.25 0.5")
    for t in idx: lines.append("f %d/1/%d %d/1/%d %d/1/%d"%(t[0]+1,t[0]+1,t[1]+1,t[1]+1,t[2]+1,t[2]+1))
    open(os.path.join(MODELS,"HandSkin.obj"),"w").write("\n".join(lines)+"\n")
    with open(os.path.join(MODELS,"HandSkin.bytes"),"wb") as f:
        f.write(struct.pack("<ii",len(verts),len(idx)))
        f.write(verts.astype("<f4").tobytes()); f.write(nrm.astype("<f4").tobytes())
        f.write(bi.astype("<i4").tobytes()); f.write(bw.astype("<f4").tobytes())
        f.write(idx.astype("<i4").tobytes())
    np.savez(os.path.join(ROOT,"Tools","_hand_cache.npz"),verts=verts,nrm=nrm,idx=idx,bi=bi,bw=bw)
    print("вершин",len(verts),"треугольников",len(idx))

if __name__=="__main__":
    main()
