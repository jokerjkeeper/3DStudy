from PIL import Image, ImageDraw
from pathlib import Path
import math

OUT = Path(r"D:\unity\3D\StormLabGraphVerification\Assets\StormLab\Battle\Pixels")
S = (56, 58)
K = "#18212e"
D = "#213344"
T0, T1, T2, T3 = "#254650", "#306874", "#469098", "#72adaa"
G0, G1, G2, G3 = "#684436", "#a66e40", "#dfa758", "#f3d788"
L0, L1, L2 = "#302d3e", "#5e4652", "#916761"
P0, P1, P2 = "#a15c51", "#d48967", "#efb784"
I0, I1 = "#303544", "#596678"

def poly(d, pts, color, border=None):
    d.polygon(pts, fill=color, outline=border)

def rect(d, bounds, color):
    d.rectangle(bounds, fill=color)

def line(d, pts, color, width=1):
    d.line(pts, fill=color, width=width)

def arm(d, shoulder, elbow, hand, cloth, sleeve_hi, brace, skin):
    line(d, [shoulder, elbow], K, 9)
    line(d, [elbow, hand], K, 7)
    line(d, [shoulder, elbow], cloth, 6)
    line(d, [elbow, hand], brace, 4)
    line(d, [(shoulder[0], shoulder[1]-2), (elbow[0], elbow[1]-2)], sleeve_hi, 2)
    hx, hy = hand
    poly(d, [(hx-4,hy-1),(hx-3,hy-3),(hx+1,hy-3),(hx+3,hy-1),(hx+2,hy+2),(hx-1,hy+3),(hx-3,hy+2)], skin, K)
    rect(d, (hx-3,hy-1,hx-1,hy), P2)

def caster(name, pose, hand, elbow, bob):
    im = Image.new("RGBA", S)
    d = ImageDraw.Draw(im)
    by = bob
    def q(pts, color, border=None):
        poly(d, [(x,y+by) for x,y in pts], color, border)
    # Cloak, walking boots and coat silhouette.
    cloak_tip = 46 if pose in ("send","follow") else 43
    q([(28,21),(36,22),(39,29),(40,37),(cloak_tip,47),(39,51),(27,47),(25,37)],D,K)
    q([(31,23),(37,26),(38,35),(cloak_tip-2,45),(40,48),(36,43),(34,33)],T0)
    q([(28,34),(33,39),(33,46),(30,49),(28,54),(21,54),(20,52),(23,49),(22,43)],L0,K)
    q([(31,39),(37,41),(39,48),(41,52),(39,54),(31,54),(30,51),(32,49),(30,45)],D,K)
    q([(21,51),(26,49),(29,50),(28,53),(22,53)],L1)
    q([(33,51),(38,50),(40,52),(38,53),(32,53)],L1)
    q([(23,24),(30,21),(36,24),(37,34),(35,39),(38,48),(33,50),(29,48),(25,50),(19,47),(22,38),(21,30)],T1,K)
    q([(23,27),(27,25),(29,32),(27,37),(25,45),(20,47),(23,37)],T2)
    q([(33,26),(36,28),(35,39),(36,46),(33,48),(30,45),(31,35)],T0)
    q([(26,33),(31,29),(32,30),(28,35),(27,46),(25,48)],G1)
    q([(22,36),(33,36),(35,38),(34,40),(22,39),(21,38)],L0,K)
    q([(24,37),(32,37),(32,39),(24,39)],L2)
    q([(27,37),(29,37),(29,39),(27,39)],G2)
    q([(35,39),(37,38),(40,40),(39,45),(35,45),(34,43)],G0,K)
    q([(36,40),(38,40),(38,42),(36,42)],G2)
    # Back arm and scarf tail.
    q([(33,25),(37,26),(39,33),(38,36),(35,35),(34,30)],T0,K)
    q([(30,22),(34,20),(38,24),(43,24),(44,27),(39,29),(34,26),(30,26)],G0,K)
    q([(31,22),(35,22),(39,25),(43,25),(41,27),(36,26)],G2)
    # Original hood and face, facing screen left.
    q([(19,14),(21,9),(24,6),(31,5),(36,8),(39,13),(38,20),(34,24),(23,23),(20,20)],D,K)
    q([(23,9),(29,6),(34,8),(37,13),(36,20),(33,21),(34,16),(32,12),(26,11),(21,14)],T1)
    q([(22,13),(26,10),(31,11),(32,14),(30,19),(28,22),(23,21),(22,19),(19,18),(19,16)],P1,K)
    q([(22,14),(27,12),(30,13),(29,16),(26,16),(25,20),(22,18),(20,17)],P2)
    q([(27,17),(29,17),(28,21),(24,21)],P0)
    q([(21,15),(24,14),(25,15),(24,16),(21,16)],L0)
    q([(22,15),(23,15),(23,15)],"#f1e9cc")
    q([(28,9),(32,10),(34,13),(32,18),(30,19),(31,14),(27,13)],L0)
    q([(23,22),(32,20),(35,22),(33,25),(22,25),(20,24)],G0,K)
    q([(22,23),(32,21),(34,22),(32,24),(23,24)],G2)
    q([(28,22),(33,21),(33,22),(28,23)],G3)
    shoulder = (25,26+by)
    arm(d,shoulder,elbow,hand,T1,T3,G1,P1)
    hx,hy=hand
    rect(d,(hx+1,hy+1,hx+2,hy+1),"#8cc5be")
    # Metal rivets and one gold cloak seam.
    q([(33,7),(35,8),(35,10),(33,10),(32,9)],G1)
    q([(33,8),(34,8),(34,9)],G3)
    im.save(OUT / (name + ".png"))

caster("Wayfarer Idle A","idle",(21,36),(22,33),0)
caster("Wayfarer Idle B","idle",(21,35),(22,32),-1)
caster("Wayfarer Gather","gather",(18,30),(22,32),0)
caster("Wayfarer Lift","lift",(15,23),(22,30),0)
caster("Wayfarer Channel A","channel",(13,18),(21,29),-1)
caster("Wayfarer Channel B","channel",(13,17),(22,28),-1)
caster("Wayfarer Release","send",(7,25),(16,26),1)
caster("Wayfarer Follow","follow",(9,28),(17,28),1)
caster("Wayfarer Recover","recover",(15,32),(21,33),0)

M0, M1, M2, M3 = "#2f3949","#454e5e","#647589","#9ea9ae"
B0, B1, B2 = "#784e3f", "#bd7d4e", "#e7b168"
C0,C1 = "#f4864c","#ffd88c"

def sentinel(name, recoil=0, pulse=False, flash=False):
    im = Image.new("RGBA",S)
    d = ImageDraw.Draw(im)
    def q(pts,color,border=None, upper=True):
        if upper:
            pts = [(x + round(recoil * max(0, (45-y))/34),y) for x,y in pts]
        poly(d,pts,color,border)
    # Braced feet and layered shin plates.
    q([(18,36),(27,36),(29,43),(26,47),(27,52),(24,54),(13,54),(12,51),(16,48),(16,42)],M0,K,False)
    q([(17,40),(23,39),(25,43),(23,47),(16,49),(15,46)],M2,None,False)
    q([(27,36),(35,35),(38,42),(37,47),(43,50),(43,53),(32,54),(29,51),(30,46),(27,42)],M0,K,False)
    q([(31,40),(36,39),(37,44),(35,47),(31,48),(31,45)],M2,None,False)
    q([(13,50),(19,48),(22,49),(21,51),(14,52)],B1,None,False)
    q([(32,50),(38,48),(42,50),(40,52),(33,52)],B1,None,False)
    # Core, banded arms, and broad asymmetric plates.
    q([(16,20),(33,19),(39,24),(38,34),(34,42),(26,44),(18,39),(15,31)],M1,K)
    q([(20,23),(27,23),(30,31),(27,39),(23,41),(19,38),(17,30)],M0)
    q([(30,24),(36,23),(38,26),(36,35),(32,38),(28,34),(28,28)],M2)
    q([(19,35),(30,39),(35,37),(34,41),(26,43),(20,40)],B0,K)
    q([(31,27),(35,30),(32,35),(28,31)],B0,K)
    q([(31,28),(34,30),(32,33),(30,31)],C1 if pulse else C0)
    q([(32,29),(33,30),(32,32),(31,30)],"#ffeed0")
    q([(15,20),(21,18),(25,22),(23,28),(18,29),(10,26),(10,23)],B1,K)
    q([(12,23),(17,20),(22,20),(23,22),(20,24),(12,25)],B2)
    q([(12,27),(18,28),(20,34),(17,39),(14,39),(10,36),(10,31)],M1,K)
    q([(12,29),(15,29),(17,34),(15,37),(12,36)],M2)
    q([(38,23),(42,24),(46,31),(44,35),(39,34),(38,30),(36,28)],M2,K)
    q([(41,24),(43,25),(45,29),(42,30),(39,27)],B1)
    # Furnace visor.
    q([(22,9),(31,7),(38,9),(41,13),(40,19),(35,23),(24,22),(20,19),(20,13)],M0,K)
    q([(23,10),(31,8),(38,10),(40,13),(34,15),(25,14),(22,16)],M2)
    q([(35,12),(41,12),(41,17),(38,19),(32,18),(31,15)],M1,K)
    q([(33,15),(39,14),(40,15),(39,17),(34,17)],C1 if pulse else C0)
    q([(33,15),(38,15),(38,15)],"#fff0b2")
    q([(23,19),(29,20),(34,21),(32,22),(24,21)],B1)
    if flash:
        for y in range(S[1]):
            for x in range(S[0]):
                p=im.getpixel((x,y))
                if p[3] and p[:3] != (24,33,46):
                    im.putpixel((x,y), tuple(int(v*0.52+c*0.48) for v,c in zip(p[:3],(255,214,165)))+(255,))
    im.save(OUT / (name + ".png"))

sentinel("Sentinel Idle A")
sentinel("Sentinel Idle B",pulse=True)
sentinel("Sentinel Impact",-5,True,True)
sentinel("Sentinel Recoil",-7)
sentinel("Sentinel Recover",-3,True)

shadow = Image.new("RGBA",(32,32))
for y in range(32):
    for x in range(32):
        r=math.hypot((x-15.5)/15.3,(y-15.5)/15.3)
        alpha=int(150*max(0,1-r*r)**2)
        shadow.putpixel((x,y),(16,21,25,alpha))
shadow.save(OUT/"Pixel Contact Shadow.png")

