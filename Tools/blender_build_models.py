# Сборка 3D-моделей проекта в Blender и экспорт в FBX.
# Запуск (Blender 3.x/4.x):  blender --background --python Tools/blender_build_models.py
# Результат: Blender/SupermarketModels.blend (все модели в ряд, с текстурами) и Assets/Models/FBX/*.fbx.
# Скрипт не изменяет игру; он нужен, чтобы показать/дорабатывать модели в Blender.
import bpy, os, glob, math

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MODELS = os.path.join(ROOT, "Assets", "Models")
TEX = os.path.join(ROOT, "Assets", "Textures")
OUT_BLEND = os.path.join(ROOT, "Blender", "SupermarketModels.blend")
OUT_FBX = os.path.join(MODELS, "FBX")
os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)
os.makedirs(OUT_FBX, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

def import_obj(path):
    if hasattr(bpy.ops.wm, "obj_import"):          # Blender 3.2+
        bpy.ops.wm.obj_import(filepath=path)
    else:
        bpy.ops.import_scene.obj(filepath=path)

def find_texture(name):
    for cand in ("Tex_" + name + ".png", "Tex_Person_" + name.replace("Person_", "") + ".png"):
        p = os.path.join(TEX, cand)
        if os.path.exists(p):
            return p
    return None

def material_with_texture(name, tex_path):
    mat = bpy.data.materials.new("M_" + name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if tex_path and bsdf:
        img = nodes.new("ShaderNodeTexImage")
        img.image = bpy.data.images.load(tex_path)
        mat.node_tree.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    return mat

x = 0.0
for obj_file in sorted(glob.glob(os.path.join(MODELS, "*.obj"))):
    name = os.path.splitext(os.path.basename(obj_file))[0]
    before = set(bpy.data.objects)
    import_obj(obj_file)
    new = [o for o in bpy.data.objects if o not in before]
    if not new:
        continue
    mat = material_with_texture(name, find_texture(name))
    for o in new:
        o.name = name if len(new) == 1 else name + "." + o.name
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(mat)
        o.location.x += x
    # экспорт каждой модели в FBX
    bpy.ops.object.select_all(action="DESELECT")
    for o in new:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, name + ".fbx"), use_selection=True, apply_unit_scale=True)
    dims = max(max(o.dimensions) for o in new if o.type == "MESH") if any(o.type == "MESH" for o in new) else 1.0
    x += max(1.0, dims + 0.5)

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("Готово:", OUT_BLEND)
