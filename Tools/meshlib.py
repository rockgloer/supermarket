# Мини-библиотека для генерации 3D-моделей (OBJ с UV и нормалями)
import math, os
import numpy as np

def sub(a,b): return (a[0]-b[0],a[1]-b[1],a[2]-b[2])
def add(a,b): return (a[0]+b[0],a[1]+b[1],a[2]+b[2])
def cross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def dot(a,b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]
def norm(a):
    l = math.sqrt(dot(a,a)) or 1.0
    return (a[0]/l,a[1]/l,a[2]/l)

class Part:
    """polys: список полигонов [(x,y,z)...]; uvs: список списков (u,v); smooth: сглаженные нормали"""
    def __init__(self, polys, uvs, smooth=False):
        self.polys, self.uvs, self.smooth = polys, uvs, smooth

def _orient(poly, uv, ref):
    n = cross(sub(poly[1],poly[0]), sub(poly[2],poly[0]))
    fc = tuple(sum(p[i] for p in poly)/len(poly) for i in range(3))
    if dot(n, sub(fc, ref)) < 0:
        return poly[::-1], uv[::-1]
    return poly, uv

def _finish(polys, uvs, ref, smooth):
    out_p, out_u = [], []
    for p,u in zip(polys,uvs):
        # пропускаем вырожденные полигоны
        n = cross(sub(p[1],p[0]), sub(p[2],p[0]))
        if dot(n,n) < 1e-14: continue
        p,u = _orient(p,u,ref)
        out_p.append(p); out_u.append(u)
    return Part(out_p, out_u, smooth)

def cellmap(cell):
    """cell=(u0,v0,u1,v1) -> функция (u,v)->UV внутри ячейки текстуры"""
    if cell is None: return lambda u,v:(u,v)
    u0,v0,u1,v1 = cell
    return lambda u,v:(u0+u*(u1-u0), v0+v*(v1-v0))

def box(cx,cy,cz,sx,sy,sz,cell=None):
    m = cellmap(cell)
    x0,x1=cx-sx/2,cx+sx/2; y0,y1=cy-sy/2,cy+sy/2; z0,z1=cz-sz/2,cz+sz/2
    faces = []
    # (+z) лицо
    faces.append(([(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)], [(0,0),(1,0),(1,1),(0,1)]))
    # (-z)
    faces.append(([(x1,y0,z0),(x0,y0,z0),(x0,y1,z0),(x1,y1,z0)], [(0,0),(1,0),(1,1),(0,1)]))
    # (+x)
    faces.append(([(x1,y0,z1),(x1,y0,z0),(x1,y1,z0),(x1,y1,z1)], [(0,0),(1,0),(1,1),(0,1)]))
    # (-x)
    faces.append(([(x0,y0,z0),(x0,y0,z1),(x0,y1,z1),(x0,y1,z0)], [(0,0),(1,0),(1,1),(0,1)]))
    # верх
    faces.append(([(x0,y1,z1),(x1,y1,z1),(x1,y1,z0),(x0,y1,z0)], [(0,0),(1,0),(1,1),(0,1)]))
    # низ
    faces.append(([(x0,y0,z0),(x1,y0,z0),(x1,y0,z1),(x0,y0,z1)], [(0,0),(1,0),(1,1),(0,1)]))
    polys=[f[0] for f in faces]; uvs=[[m(*q) for q in f[1]] for f in faces]
    return _finish(polys, uvs, (cx,cy,cz), False)

def lathe(profile, seg=16, ax=1.0, az=1.0, cell=None, caps=True, center=(0,0,0), ref=None, smooth=True, uv_u_scale=1.0):
    """Тело вращения вокруг оси Y. profile=[(r,y)...] снизу вверх. ax/az — растяжение по x/z (эллипс)."""
    m = cellmap(cell)
    n = len(profile)
    cx,cy,cz = center
    def P(i,j):
        r,y = profile[i]; a = 2*math.pi*j/seg
        return (cx+ax*r*math.cos(a), cy+y, cz+az*r*math.sin(a))
    def UV(i,j): return m(1.0-(j/seg)*uv_u_scale, i/(n-1))
    polys=[]; uvs=[]
    for i in range(n-1):
        for j in range(seg):
            r0,r1 = profile[i][0], profile[i+1][0]
            a=(P(i,j),UV(i,j)); b=(P(i+1,j),UV(i+1,j)); c=(P(i+1,j+1),UV(i+1,j+1)); d=(P(i,j+1),UV(i,j+1))
            if r0<1e-9 and r1<1e-9: continue
            if r0<1e-9: q=[a,c,d] if False else [b,c,a]
            elif r1<1e-9: q=[a,d,b]
            else: q=[a,b,c,d]
            polys.append([t[0] for t in q]); uvs.append([t[1] for t in q])
    if caps:
        for idx in (0,n-1):
            if profile[idx][0] > 1e-9:
                ring=[P(idx,j) for j in range(seg)]
                polys.append(ring); uvs.append([m(0.5,0.5)]*seg)
    if ref is None:
        allv=[v for p in polys for v in p]
        ref=tuple(sum(v[k] for v in allv)/len(allv) for k in range(3))
    return _finish(polys, uvs, ref, smooth)

def ellipsoid(cx,cy,cz, rx,ry,rz, th0=0.0, th1=180.0, seg=20, rings=12, cell=None, caps=False, smooth=True):
    """Эллипсоид (или его часть по полярному углу th0..th1 в градусах, 0 — верх)."""
    prof=[]
    for i in range(rings+1):
        th = math.radians(th1 + (th0-th1)*i/rings)   # снизу вверх: от th1 к th0
        prof.append((math.sin(th), ry*math.cos(th)))
    part = lathe(prof, seg=seg, ax=rx, az=rz, cell=cell, caps=caps, center=(cx,cy,cz), ref=(cx,cy,cz), smooth=smooth)
    return part

def cylinder(cx,cy,cz,r,h,seg=16,axis='y',cell=None,smooth=True):
    prof=[(r,-h/2),(r,h/2)]
    p = lathe(prof,seg=seg,cell=cell,caps=True,center=(0,0,0),smooth=smooth)
    # сглаживание только по боковине: считаем «жёстко» через два профиля не нужно — для банок достаточно smooth
    if axis=='x': p = rotate(p, 'z', 90)
    return translate(p,(cx,cy,cz))

def translate(part, t):
    return Part([[add(v,t) for v in p] for p in part.polys], part.uvs, part.smooth)

def _rot(v, axis, deg):
    a=math.radians(deg); c,s=math.cos(a),math.sin(a); x,y,z=v
    if axis=='x': return (x, y*c-z*s, y*s+z*c)
    if axis=='y': return (x*c+z*s, y, -x*s+z*c)
    return (x*c-y*s, x*s+y*c, z)

def rotate(part, axis, deg, pivot=(0,0,0)):
    out=[[add(_rot(sub(v,pivot),axis,deg),pivot) for v in p] for p in part.polys]
    return Part(out, part.uvs, part.smooth)

def mirror_x(part):
    out=[[(-v[0],v[1],v[2]) for v in p][::-1] for p in part.polys]
    return Part(out, [u[::-1] for u in part.uvs], part.smooth)

def write_obj(path, name, parts):
    try:
        write_parts(path[:-4]+".parts", [(name,(0,0,0),parts)], mirror=True)
    except NameError:
        pass
    lines=["# %s"%name, "o %s"%name]
    vi=1
    for part in parts:
        # нормали
        acc={}
        fnorm=[]
        for fi,p in enumerate(part.polys):
            n=cross(sub(p[1],p[0]), sub(p[2],p[0]))
            fnorm.append(n)
            if part.smooth:
                for v in p:
                    k=(round(v[0],4),round(v[1],4),round(v[2],4))
                    a=acc.get(k,(0,0,0)); acc[k]=add(a,n)
        for fi,p in enumerate(part.polys):
            idx=[]
            for k,v in enumerate(p):
                if part.smooth:
                    nn=norm(acc[(round(v[0],4),round(v[1],4),round(v[2],4))])
                else:
                    nn=norm(fnorm[fi])
                u=part.uvs[fi][k]
                lines.append("v %.5f %.5f %.5f"%v)
                lines.append("vt %.5f %.5f"%(u[0],u[1]))
                lines.append("vn %.5f %.5f %.5f"%nn)
                idx.append(vi); vi+=1
            lines.append("f "+" ".join("%d/%d/%d"%(i,i,i) for i in idx))
    with open(path,"w",encoding="utf-8") as f: f.write("\n".join(lines)+"\n")


# ---------- экспорт частей тела для Unity (бинарный формат, не зависит от импортёра OBJ) ----------
import struct

def _flatten(part, pivot, mirror=False):
    """Часть -> массивы вершин (относительно pivot), нормалей, uv и треугольников (веер из полигонов)."""
    V=[];N=[];U=[];T=[]
    acc={}
    fnorm=[]
    for p in part.polys:
        n=cross(sub(p[1],p[0]), sub(p[2],p[0])); fnorm.append(n)
        if part.smooth:
            for v in p:
                k=(round(v[0],4),round(v[1],4),round(v[2],4)); acc[k]=add(acc.get(k,(0,0,0)),n)
    for fi,p in enumerate(part.polys):
        base=len(V)
        for k,v in enumerate(p):
            nn=norm(acc[(round(v[0],4),round(v[1],4),round(v[2],4))]) if part.smooth else norm(fnorm[fi])
            V.append((v[0]-pivot[0],v[1]-pivot[1],v[2]-pivot[2])); N.append(nn); U.append(part.uvs[fi][k])
        for k in range(1,len(p)-1): T.append((base,base+k,base+k+1))
    if mirror:
        V=[(-v[0],v[1],v[2]) for v in V]; N=[(-n[0],n[1],n[2]) for n in N]; T=[(a,c,b) for a,b,c in T]
    return V,N,U,T

def write_parts(path, groups, mirror=False):
    """groups = [(имя, pivot(x,y,z), [Part...])]"""
    out=bytearray(struct.pack("<i",len(groups)))
    for name,pivot,parts in groups:
        V=[];N=[];U=[];T=[]
        for part in parts:
            v,n,u,t=_flatten(part,pivot,mirror)
            off=len(V); V+=v; N+=n; U+=u; T+=[(a+off,b+off,c+off) for a,b,c in t]
        nb=name.encode("utf-8")
        out+=struct.pack("<i",len(nb))+nb
        out+=struct.pack("<3f",*pivot)
        out+=struct.pack("<ii",len(V),len(T))
        for a in V: out+=struct.pack("<3f",*a)
        for a in N: out+=struct.pack("<3f",*a)
        for a in U: out+=struct.pack("<2f",*a)
        for a in T: out+=struct.pack("<3i",*a)
    open(path,"wb").write(bytes(out))

def read_parts(path):
    b=open(path,"rb").read(); o=0
    n,=struct.unpack_from("<i",b,o); o+=4; res=[]
    for _ in range(n):
        L,=struct.unpack_from("<i",b,o); o+=4; name=b[o:o+L].decode("utf-8"); o+=L
        pivot=struct.unpack_from("<3f",b,o); o+=12
        nv,nt=struct.unpack_from("<ii",b,o); o+=8
        V=np.frombuffer(b,"<f4",nv*3,o).reshape(-1,3); o+=nv*12
        N=np.frombuffer(b,"<f4",nv*3,o).reshape(-1,3); o+=nv*12
        U=np.frombuffer(b,"<f4",nv*2,o).reshape(-1,2); o+=nv*8
        T=np.frombuffer(b,"<i4",nt*3,o).reshape(-1,3); o+=nt*12
        res.append((name,np.array(pivot),V,N,U,T))
    return res
