@tool
extends  Node3D
class_name DesertPlanetGenerator

@export var radius: float = 50
@export var mesh_resolution: int = 5
@export var noise_height_multiplication: float = 2

@export var mesh_instance_3d: MeshInstance3D
@export var noise: FastNoiseLite
@export var material: StandardMaterial3D

@export_tool_button("Generate New Map") var generate_action = generate

var _surface_tool: SurfaceTool

func _ready():
	generate()

func create_new_mesh():
	var sphere_mesh: SphereMesh = SphereMesh.new()
	
	sphere_mesh.radius = radius
	sphere_mesh.height = radius * 2.0
	sphere_mesh.radial_segments = mesh_resolution * 2
	sphere_mesh.rings = mesh_resolution
	sphere_mesh.material = material
	
	_surface_tool = SurfaceTool.new()
	_surface_tool.create_from(sphere_mesh, 0)
	
	mesh_instance_3d.mesh = _surface_tool.commit()

func generate():
	create_new_mesh()
	
	noise.seed = randi()
	
	var data: MeshDataTool = MeshDataTool.new()
	var array_mesh = _surface_tool.commit()
	data.create_from_surface(array_mesh, 0)
	
	for i in range(data.get_vertex_count()):
		var vertex: Vector3 = data.get_vertex(i)
		var noise_value = noise.get_noise_3dv(vertex)
		var normal = vertex.normalized()
		
		vertex += normal * noise_value * noise_height_multiplication
		data.set_vertex(i, vertex)
	
	array_mesh.clear_surfaces()
	data.commit_to_surface(array_mesh)
	
	_surface_tool.clear()
	_surface_tool.create_from(array_mesh, 0)
	_surface_tool.generate_normals()
	
	mesh_instance_3d.mesh = _surface_tool.commit()
