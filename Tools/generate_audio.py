# Синтез звуков (WAV, 22050 Гц, моно) и текстур частиц. Запуск: python3 generate_audio.py
import os, wave, math
import numpy as np
from PIL import Image

ROOT=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..")
AUD=os.path.join(ROOT,"Assets","Audio"); TEX=os.path.join(ROOT,"Assets","Textures")
SR=22050
rng=np.random.RandomState(3)

def t(d): return np.arange(int(SR*d))/SR
def env(n,a=0.005,r=0.05):
    e=np.ones(n); na=max(1,int(a*SR)); nr=max(1,int(r*SR))
    e[:na]=np.linspace(0,1,na); e[-nr:]=np.minimum(e[-nr:],np.linspace(1,0,nr)); return e
def decay(x,k): return np.exp(-k*x)
def lowpass(x,a=0.15):
    y=np.zeros_like(x); s=0.0
    av=np.broadcast_to(np.asarray(a,float),x.shape)
    for i,v in enumerate(x): s+=av[i]*(v-s); y[i]=s
    return y
def save(name,x,vol=0.9):
    x=np.asarray(x,float); m=np.max(np.abs(x))+1e-9; x=x/m*vol
    with wave.open(os.path.join(AUD,name+".wav"),"wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x*32767).astype("<i2").tobytes())

# --- звуки взаимодействия
x=t(0.07); save("grab", (np.sin(2*np.pi*900*x)*decay(x,60)+0.4*rng.randn(len(x))*decay(x,90))*env(len(x),0.001,0.01))
x=t(0.18); save("drop",(np.sin(2*np.pi*(140-60*x/0.18)*x)*decay(x,18)+0.3*lowpass(rng.randn(len(x)),0.3)*decay(x,25)))
x=t(0.10); save("thud",(np.sin(2*np.pi*180*x)*decay(x,35)+0.25*lowpass(rng.randn(len(x)),0.25)*decay(x,40)),0.7)
x=t(0.10); save("beep",np.sign(np.sin(2*np.pi*1800*x))*0.5*env(len(x),0.003,0.02)+np.sin(2*np.pi*1800*x)*0.5*env(len(x),0.003,0.02),0.6)
x=t(0.35); save("error",(np.sign(np.sin(2*np.pi*190*x))*0.5+np.sin(2*np.pi*195*x)*0.5)*env(len(x),0.01,0.08),0.7)
x=t(1.0); bell=sum(np.sin(2*np.pi*f*x)*a for f,a in ((2093,1),(3136,0.6),(4186,0.35),(5230,0.2)))*decay(x,4.5)
click=lowpass(rng.randn(len(x)),0.5)*decay(x,120)*0.8; save("ching",bell+click)
x=t(0.35); sw=lowpass(rng.randn(len(x)),0.08+0.25*np.sin(np.pi*x/0.35)); save("drawer",sw*np.sin(np.pi*x/0.35)**0.7)
x=t(0.7); sw=lowpass(rng.randn(len(x)),0.04+0.3*np.sin(np.pi*x/0.7)**2); save("door",sw*np.sin(np.pi*x/0.7)**1.2,0.6)
x=t(0.3); f=400+1400*(x/0.3)**1.5; save("teleport",np.sin(2*np.pi*np.cumsum(f)/SR)*np.sin(np.pi*x/0.3)**0.8,0.6)
x=t(0.09); save("step",lowpass(rng.randn(len(x)),0.12)*decay(x,45)+0.5*np.sin(2*np.pi*70*x)*decay(x,50),0.7)
x=t(0.05); save("click",np.sin(2*np.pi*1300*x)*decay(x,70)*env(len(x),0.001,0.01),0.6)
x=t(0.45); save("hello",(np.sin(2*np.pi*659*x)*(x<0.2)*decay(x%0.2,10)+np.sin(2*np.pi*880*x)*(x>=0.2)*decay((x-0.2).clip(0),9))*env(len(x),0.003,0.05),0.6)
x=t(1.2); notes=[523,659,784,1046]; y=np.zeros(len(x))
for i,fq in enumerate(notes):
    s=int(i*0.18*SR); xx=np.arange(len(x)-s)/SR; y[s:]+=np.sin(2*np.pi*fq*xx)*decay(xx,3)
save("fanfare",y)

# --- фоновая музыка (петля 32 с): мягкие аккорды + бас + арпеджио
def chord(freqs,d):
    x=t(d); y=np.zeros(len(x))
    for f in freqs:
        y+=np.sin(2*np.pi*f*x)+0.3*np.sin(2*np.pi*2*f*x)+0.12*np.sin(2*np.pi*3*f*x)
    return y/len(freqs)*env(len(x),0.4,0.6)
prog=[([261.6,329.6,392.0,493.9],65.4),([220.0,261.6,329.6,392.0],55.0),([174.6,220.0,261.6,329.6],43.65),([196.0,246.9,293.7,349.2],49.0)]
music=[]
for fr,bass in prog:
    bar=chord(fr,8.0)*0.5
    xb=t(8.0); bar+=np.sin(2*np.pi*bass*xb)*0.35*(0.6+0.4*np.sin(2*np.pi*0.5*xb))
    for k in range(16):
        s=int(k*0.5*SR); f=fr[k%4]*2; xx=np.arange(int(0.45*SR))/SR
        n=np.sin(2*np.pi*f*xx)*decay(xx,9)*0.18
        bar[s:s+len(n)]+=n
    music.append(bar)
m=np.concatenate(music); m=np.convolve(m,np.ones(3)/3,mode="same"); save("music",m,0.55)
# --- гул магазина (петля 8 с)
x=t(8.0); hum=np.sin(2*np.pi*60*x)*0.25+np.sin(2*np.pi*120*x)*0.1+lowpass(rng.randn(len(x)),0.03)*0.8
fade=int(0.5*SR); hum[:fade]=hum[:fade]*np.linspace(0,1,fade)+hum[-fade:]*np.linspace(1,0,fade)
save("ambience",hum,0.4)

# --- текстуры частиц
def radial(size,power):
    y,xg=np.mgrid[0:size,0:size]; r=np.sqrt((xg-size/2+0.5)**2+(y-size/2+0.5)**2)/(size/2)
    return np.clip(1-r,0,1)**power
a=radial(64,1.6); img=np.zeros((64,64,4),np.uint8); img[...,:3]=255; img[...,3]=(a*255).astype(np.uint8); Image.fromarray(img).save(os.path.join(TEX,"Tex_Particle.png"))
size=64; y,xg=np.mgrid[0:size,0:size]; dx=np.abs(xg-size/2+0.5)/(size/2); dy=np.abs(y-size/2+0.5)/(size/2)
star=np.clip(1-(dx**0.5+dy**0.5)**2*0.9,0,1)**1.2; star=np.maximum(star,radial(64,2.5)*0.8)
img=np.zeros((64,64,4),np.uint8); img[...,:3]=255; img[...,3]=(star*255).astype(np.uint8); Image.fromarray(img).save(os.path.join(TEX,"Tex_Spark.png"))
leaf=np.zeros((64,64,4),np.uint8)
for i in range(64):
    for j in range(64):
        u=(j-32)/14.0; v=(i-32)/26.0
        if u*u+v*v<1.0 and abs(u)<(1-abs(v))**0.6+0.0: leaf[i,j]=(90,170,60,255)
leaf[28:36,30:34]=(60,120,40,255); Image.fromarray(leaf).save(os.path.join(TEX,"Tex_Leaf.png"))
print("звуков:",len([f for f in os.listdir(AUD) if f.endswith(".wav")]))
