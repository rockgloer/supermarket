# Генерация текстур (PNG): люди, упаковки продуктов, пол
import os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import matplotlib

FONT_B = os.path.join(matplotlib.get_data_path(), "fonts", "ttf", "DejaVuSans-Bold.ttf")
rng = np.random.RandomState(5)

def font(size): return ImageFont.truetype(FONT_B, size)
def rgb(c): return tuple(int(round(max(0,min(1,x))*255)) for x in c)

def noise_img(w, h, amp=10, scale=8):
    small = rng.randn(max(2,h//scale), max(2,w//scale))
    im = Image.fromarray(((small*amp)+128).clip(0,255).astype(np.uint8)).resize((w,h), Image.BICUBIC)
    return (np.asarray(im).astype(np.float32)-128.0)

def fill_noise(img, box, color, amp=8, scale=6):
    x0,y0,x1,y1 = box
    w,h = x1-x0, y1-y0
    base = np.zeros((h,w,3),np.float32); base[:] = rgb(color)
    n = noise_img(w,h,amp,scale)[:,:,None]
    arr = (base+n).clip(0,255).astype(np.uint8)
    img.paste(Image.fromarray(arr), (x0,y0))

def text_center(img, text, cx, cy, size, fill, squeeze=1.0, maxw=None):
    f = font(size)
    # подгоняем размер под ширину
    while True:
        tmp = Image.new("RGBA",(10,10)); d = ImageDraw.Draw(tmp)
        bb = d.textbbox((0,0),text,font=f)
        w,h = bb[2]-bb[0], bb[3]-bb[1]
        if maxw is None or w*squeeze <= maxw or size<=10: break
        size -= 2; f = font(size)
    layer = Image.new("RGBA",(w+8,h+16),(0,0,0,0)); d = ImageDraw.Draw(layer)
    d.text((4-bb[0],4-bb[1]), text, font=f, fill=fill)
    if squeeze != 1.0:
        layer = layer.resize((max(1,int(layer.width*squeeze)), layer.height), Image.BICUBIC)
    img.paste(layer, (int(cx-layer.width/2), int(cy-layer.height/2)), layer)

def pack_texture(path, text, bg, band, fg, wrap=False, size=256):
    """Упаковка: фон, цветная полоса, надпись. wrap=True — для цилиндров (надпись сжата и повторена)."""
    img = Image.new("RGB",(size,size), rgb(bg))
    fill_noise(img,(0,0,size,size),bg,amp=5,scale=4)
    d = ImageDraw.Draw(img)
    d.rectangle((0,int(size*0.34),size,int(size*0.70)), fill=rgb(band))
    d.rectangle((0,0,size,int(size*0.10)), fill=rgb(band))
    d.rectangle((0,int(size*0.90),size,size), fill=rgb(band))
    if wrap:
        for cx in (size*0.25, size*0.75):
            text_center(img, text, cx, size*0.52, 60, rgb(fg)+(255,), squeeze=0.5, maxw=size*0.40)
    else:
        text_center(img, text, size/2, size*0.52, 64, rgb(fg)+(255,), maxw=size*0.88)
    # лёгкий блик
    img.save(path)

def fruit_texture(path, a, b):
    size=256
    arr=np.zeros((size,size,3),np.float32)
    for y in range(size):
        t=y/size
        c=np.array(a)*(1-t)+np.array(b)*t
        arr[y,:,:]=c*255
    arr += noise_img(size,size,10,8)[:,:,None]
    Image.fromarray(arr.clip(0,255).astype(np.uint8)).save(path)

def bread_texture(path):
    size=256
    img=Image.new("RGB",(size,size),(205,140,70)); fill_noise(img,(0,0,size,size),(0.80,0.54,0.27),amp=14,scale=5)
    d=ImageDraw.Draw(img)
    for x in range(30,size,48): d.line((x,40,x+20,size-40),fill=(150,90,40),width=5)
    img.save(path)

def money_texture(path):
    img=Image.new("RGB",(256,256),(120,190,120)); d=ImageDraw.Draw(img)
    d.rectangle((8,8,248,248),outline=(40,100,50),width=6)
    d.ellipse((88,88,168,168),outline=(40,100,50),width=5)
    text_center(img,"100",128,128,56,(40,100,50,255))
    img.save(path)

def floor_texture(path):
    size=512; img=Image.new("RGB",(size,size)); d=ImageDraw.Draw(img)
    for i in range(2):
        for j in range(2):
            c=(205,205,198) if (i+j)%2==0 else (170,172,170)
            d.rectangle((i*256,j*256,i*256+255,j*256+255),fill=c)
    arr=np.asarray(img).astype(np.float32)+noise_img(size,size,5,4)[:,:,None]
    img=Image.fromarray(arr.clip(0,255).astype(np.uint8)); d=ImageDraw.Draw(img)
    for k in (0,256): d.line((k,0,k,size),fill=(120,120,120),width=3); d.line((0,k,size,k),fill=(120,120,120),width=3)
    img.save(path)

# ---------- атлас человека ----------
# сетка 4x4 по 256 px (v снизу вверх). Голова: колонки 0-1, ряды 2-3 (512x512 слева сверху)
CELLS = {
    "head":  (0.0,0.5,0.5,1.0),
    "hair":  (0.5,0.75,0.75,1.0),
    "shirt": (0.75,0.75,1.0,1.0),
    "pants": (0.5,0.5,0.75,0.75),
    "skin":  (0.75,0.5,1.0,0.75),
    "shoes": (0.0,0.25,0.25,0.5),
    "accent":(0.25,0.25,0.5,0.5),
}
def cell_px(name, S=1024):
    u0,v0,u1,v1 = CELLS[name]
    return (int(u0*S), int((1-v1)*S), int(u1*S), int((1-v0)*S))

def person_atlas(path, p):
    S=1024; img=Image.new("RGB",(S,S),(128,128,128))
    fill_noise(img,cell_px("skin"),p["skin"],amp=4,scale=10)
    # волосы
    x0,y0,x1,y1=cell_px("hair"); fill_noise(img,(x0,y0,x1,y1),p["hair"],amp=14,scale=3)
    d=ImageDraw.Draw(img)
    for x in range(x0,x1,6):
        sh=random.Random(x).randint(-18,18)
        c=tuple(max(0,min(255,v+sh)) for v in rgb(p["hair"])); d.line((x,y0,x,y1),fill=c,width=3)
    # рубашка
    x0,y0,x1,y1=cell_px("shirt"); fill_noise(img,(x0,y0,x1,y1),p["shirt"],amp=6,scale=4)
    d=ImageDraw.Draw(img); st=p.get("pattern","plain"); c2=rgb(p["shirt2"])
    if st=="stripes":
        for y in range(y0,y1,40): d.rectangle((x0,y,x1,y+19),fill=c2)
    elif st=="plaid":
        for y in range(y0,y1,48): d.rectangle((x0,y,x1,y+14),fill=c2)
        for x in range(x0,x1,48): d.rectangle((x,y0,x+14,y1),fill=c2)
    elif st=="polo":
        d.rectangle((x0,y0,x1,y0+34),fill=c2)
    # штаны/юбка (pants)
    fill_noise(img,cell_px("pants"),p["pants"],amp=12,scale=2)
    # обувь
    fill_noise(img,cell_px("shoes"),p["shoes"],amp=6,scale=5)
    # акцент (юбка/кепка)
    x0,y0,x1,y1=cell_px("accent"); fill_noise(img,(x0,y0,x1,y1),p["accent"],amp=6,scale=4)
    d=ImageDraw.Draw(img)
    if p.get("accent_dots"):
        for y in range(y0+16,y1,40):
            for x in range(x0+16,x1,40): d.ellipse((x-5,y-5,x+5,y+5),fill=rgb(p["accent2"]))
    # лицо
    hx0,hy0,hx1,hy1=cell_px("head"); W=hx1-hx0; H=hy1-hy0
    fill_noise(img,(hx0,hy0,hx1,hy1),p["skin"],amp=3,scale=12)
    d=ImageDraw.Draw(img)
    ppd_x=W/360.0; ppd_y=H/180.0
    def pos(az_off, lat_deg):    # az_off — градусы от центра лица; lat_deg — угол от макушки
        return (hx0+W*0.75+az_off*ppd_x, hy0+lat_deg*ppd_y)
    # щёки
    for s in (-1,1):
        cx,cy=pos(s*24,98); d.ellipse((cx-9*ppd_x*1.6,cy-5*ppd_y,cx+9*ppd_x*1.6,cy+5*ppd_y),fill=tuple(min(255,v+0) for v in rgb(p["blush"])))
    # глаза
    for s in (-1,1):
        cx,cy=pos(s*17,82)
        rx,ry=7.5*ppd_x,7.5*ppd_y
        d.ellipse((cx-rx,cy-ry,cx+rx,cy+ry),fill=(250,250,250),outline=(60,40,40),width=2)
        irx,iry=4.6*ppd_x,4.6*ppd_y
        d.ellipse((cx-irx,cy-iry,cx+irx,cy+iry),fill=rgb(p["eyes"]))
        prx,pry=2.4*ppd_x,2.4*ppd_y
        d.ellipse((cx-prx,cy-pry,cx+prx,cy+pry),fill=(15,15,20))
        d.ellipse((cx-irx*0.2+3,cy-iry*0.6,cx+irx*0.2+3,cy-iry*0.1),fill=(255,255,255))
        bx,by=pos(s*17,66)
        d.line((bx-9*ppd_x,by+(s*0),bx+9*ppd_x,by-2),fill=rgb(p["brow"]),width=5)
    # рот
    mx,my=pos(0,113)
    pts=[]
    for k in range(-10,11):
        t=k/10.0
        pts.append((mx+t*11*ppd_x, my+ (1-t*t)*7))
    d.line(pts,fill=rgb(p["lips"]),width=6)
    img.save(path)
