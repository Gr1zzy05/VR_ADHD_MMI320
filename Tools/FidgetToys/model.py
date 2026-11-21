"""Run with Blender 3.5 in background mode. Dimensions below are in metres."""
import bpy
from math import sin, cos, pi, sqrt, atan2, exp
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt
from pathlib import Path

root = Path(__file__).resolve().parents[2]
assets = root / "Assets/FidgetToys"
source = root / "Tools/FidgetToys"
names = ["Violet", "Lagoon", "Leaf", "Lemon", "Tangerine", "Coral"]
palette = [(0.48,.14,.72),(.025,.71,.89),(.17,.81,.29),(.99,.84,.12),(1,.48,.12),(.94,.16,.17)]
def material(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb,1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (*rgb,1)
    bs.inputs["Roughness"].default_value = .57
    return m
def mesh(name, v, faces, mat, smooth=True):
    m = bpy.data.meshes.new(name)
    m.from_pydata(v, [], faces)
    m.update()
    obj = bpy.data.objects.new(name,m)
    bpy.context.collection.objects.link(obj)
    m.materials.append(mat)
    for p in m.polygons: p.use_smooth = smooth
    return obj
def export(name):
    folder = assets / "Models" / name
    folder.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source / (name+".blend")))
    bpy.ops.export_scene.fbx(filepath=str(folder / (name+".fbx")),object_types={'MESH'},use_mesh_modifiers=False,
        bake_anim=False, add_leaf_bones=False, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',axis_up='Y')
def butter():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = material("ButterPrint",(1,.79,.12))
    image = bpy.data.images.load(str(assets/"Materials/ButterPrint.png"))
    image.pack()
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"],mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    half = (.08,.03,.02)
    steps = (48,22,16)
    radius = .007
    verts, faces, uvs, indices = [],[],[],{}
    for axis in range(3):
        u,v = (axis+1)%3,(axis+2)%3
        for sign in (-1,1):
            grid = {}
            for i in range(steps[u]+1):
                for j in range(steps[v]+1):
                    idx = [0,0,0]
                    idx[axis] = steps[axis] if sign==1 else 0
                    idx[u],idx[v] = i,j
                    key = tuple(idx)
                    if key not in indices:
                        p = Vector([(2*k/n-1)*a for k,n,a in zip(idx,steps,half)])
                        inner = Vector([max(-a+radius,min(a-radius,c)) for c,a in zip(p,half)])
                        p = inner + (p-inner).normalized()*radius
                        indices[key]=len(verts)
                        verts.append(tuple(p))
                    grid[i,j]=indices[key]
            for i in range(steps[u]):
                for j in range(steps[v]):
                    f = [grid[i,j],grid[i+1,j],grid[i+1,j+1],grid[i,j+1]]
                    faces.append(f if sign==1 else f[::-1])
                    uvs.append(axis==2 and sign==1)
    obj=mesh("Butter",verts,faces,mat)
    uv=obj.data.uv_layers.new(name="Print")
    for poly,top in zip(obj.data.polygons,uvs):
        for li in poly.loop_indices:
            x,y,z=verts[obj.data.loops[li].vertex_index]
            uv.data[li].uv=(x/.16+.5,y/.06+.5) if top else (.025+x*.1,.025+z*.1)
    obj.shape_key_add(name="Basis")
    key=obj.shape_key_add(name="Squeeze")
    for pt,(x,y,z) in zip(key.data,verts):
        weight=.27+.73*exp(-(x/.045)**2)
        pt.co=(x*(1+.055*weight), y*(1+.24*weight),z*(1-.57*weight))
    export("ButterSquishy")

def pop():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats=[material(n,c) for n,c in zip(names,palette)]
    # Six clipped bands with actual holes through both faces of the frame.
    outline=[(-.050,.105),(.050,.105),(.105,.05),(.105,-.05),(.05,-.105),(-.05,-.105),(-.105,-.05),(-.105,.05)]
    rounded=[]
    for i,b in enumerate(outline):
        b=Vector(b)
        a=b+(Vector(outline[i-1])-b).normalized()*.004
        c=b+(Vector(outline[(i+1)%8])-b).normalized()*.004
        for j in range(7):
            t=j/6
            rounded.append(tuple((1-t)**2*a+2*t*(1-t)*b+t*t*c))
    def clip(poly,y,sign):
        result=[]
        for i,b in enumerate(poly):
            a=poly[i-1]
            if (a[1]-y)*sign*(b[1]-y)*sign<0:
                t=(y-a[1])/(b[1]-a[1])
                result.append((a[0]+(b[0]-a[0])*t,y))
            if (b[1]-y)*sign>=0: result.append(b)
        return result
    frame=[]
    def tube(name,path,r,mat,closed):
        vs=[]; fs=[]; n=len(path)
        for i,p in enumerate(path):
            prev=path[(i-1)%n] if closed or i else p
            nxt=path[(i+1)%n] if closed or i<n-1 else p
            d=(Vector(nxt)-Vector(prev)).normalized()
            for j in range(8):
                a=2*pi*j/8
                vs.append((p[0]+r*d.y*cos(a),p[1]-r*d.x*cos(a),p[2]+r*sin(a)))
        for i in range(n if closed else n-1):
            for j in range(8):
                k=(j+1)%8; m=(i+1)%n
                fs.append((i*8+j,m*8+j,m*8+k,i*8+k))
        frame.append(mesh(name,vs,fs,mat))
    for row,count in enumerate([3,5,6,6,5,3]):
        hi=.105-row*.035; lo=hi-.035; cy=(hi+lo)/2
        contour=clip(clip(rounded,hi,-1),lo,1)
        centers=[(i-(count-1)/2)*.0333 for i in range(count)]
        R=.01455
        loops=[contour]+[[(x+R*cos(2*pi*j/32),cy+R*sin(2*pi*j/32)) for j in range(32)] for x in centers]
        pts=[]; edges=[]
        for loop in loops:
            base=len(pts); pts.extend(map(Vector,loop))
            edges.extend((base+j,base+(j+1)%len(loop)) for j in range(len(loop)))
        points,_,tris,*_=delaunay_2d_cdt(pts,edges,[],0,1e-8)
        vs=[(p.x,p.y,z) for z in (.0014,-.0014) for p in points]
        n=len(points); fs=[]
        for tri in tris:
            p=sum((points[k] for k in tri),Vector((0,0)))/3
            if any((p.x-x)**2+(p.y-cy)**2<(R*.995)**2 for x in centers):continue
            fs.extend([tuple(tri),tuple(n+k for k in reversed(tri))])
        frame.append(mesh("Band",vs,fs,mats[row],False))
        # Sweep rounded silicone edging along the exterior, including both sides.
        for i,b in enumerate(contour):
            a=contour[i-1]
            if abs(b[1]-a[1])<1e-7 and abs(b[1])<.104:continue
            dx,dy=b[0]-a[0],b[1]-a[1]
            if dx*dx+dy*dy<1e-14:continue
            frame.append(mesh("Edge",[(a[0],a[1],-.003),(b[0],b[1],-.003),(b[0],b[1],.003),(a[0],a[1],.003)],[(3,2,1,0)],mats[row],False))
            for z in (-.003,.003): tube("Lip",[(*a,z),(*b,z)],.0018,mats[row],False)
        if row<5:
            xs=[p[0] for p in contour if abs(p[1]-lo)<1e-6]
            for z in (-.0018,.0018):tube("Rail",[(min(xs),lo,z),(max(xs),lo,z)],.0016,mats[row],False)
        for col,x in enumerate(centers):
            wall=[(x+R*cos(2*pi*j/32),cy+R*sin(2*pi*j/32),z)
                  for z in (-.0014,.0014) for j in range(32)]
            frame.append(mesh("Inner wall",wall,[(j,j+32,(j+1)%32+32,(j+1)%32) for j in range(32)],mats[row]))
            for z in (-.0015,.0015):
                tube("Collar",[(x+(R+.0005)*cos(2*pi*j/32),cy+(R+.0005)*sin(2*pi*j/32),z) for j in range(32)],.0011,mats[row],True)
            vs=[];fs=[];delta=[]
            for side in (1,-1):
                idx=len(vs);vs.append((0,0,.009+side*.00055));delta.append(.009)
                for ring in range(1,9):
                    rr=R*ring/8
                    height=.009*(1-(rr/R)**2)
                    for j in range(32):
                        a=2*pi*j/32
                        vs.append((rr*cos(a),rr*sin(a),height+side*.00055));delta.append(height)
                        k=idx+1+(ring-1)*32+j; nn=idx+1+(ring-1)*32+(j+1)%32
                        face=(idx,k,nn) if ring==1 else (k-32,k,nn,nn-32)
                        fs.append(face if side==1 else face[::-1])
            for j in range(32):
                a=1+7*32+j;b=1+7*32+(j+1)%32
                fs.append((a,a+257,b+257,b))
            obj=mesh(f"Bubble_{row+1}_{col+1}",vs,fs,mats[row])
            obj.location=(x,cy,0)
            obj.shape_key_add(name="Basis")
            key=obj.shape_key_add(name="Invert")
            for pt,h in zip(key.data,delta):pt.co.z-=2*h
    bpy.ops.object.select_all(action='DESELECT')
    for ob in frame:ob.select_set(True)
    bpy.context.view_layer.objects.active=frame[0]
    bpy.ops.object.join()
    bpy.context.object.name="SiliconeFrame"
    export("RainbowPopIt")
butter()
pop()
