@tool
extends  Node3D
class_name DesertGenerator

@export var size_depth: int = 100
@export var size_width: int = 100
@export var mesh_resolution: int = 3

@export var mesh_instance_3d: MeshInstance3D
@export var noise: FastNoiseLite
@export var material: StandardMaterial3D

@export_tool_button("Generate New Map") var generate_action = generate

var _surface_tool: SurfaceTool

func _ready():
	generate()

func create_new_mesh():
	var plane_mesh = PlaneMesh.new()
	
	plane_mesh.size = Vector2(size_width, size_depth)
	plane_mesh.subdivide_depth = size_depth * mesh_resolution
	plane_mesh.subdivide_width = size_width * mesh_resolution
	plane_mesh.material = material
	
	_surface_tool = SurfaceTool.new()
	_surface_tool.create_from(plane_mesh, 0)
	
	mesh_instance_3d.mesh = _surface_tool.commit()

func generate():
	create_new_mesh()
	
	noise.seed = randi()
	
	var data: MeshDataTool = MeshDataTool.new()
	var array_plane = _surface_tool.commit()
	data.create_from_surface(array_plane, 0)
	
	for i in range(data.get_vertex_count()):
		var vertex: Vector3 = data.get_vertex(i)
		vertex.y = noise.get_noise_2d(vertex.x, vertex.z)
		data.set_vertex(i, vertex)
	
	array_plane.clear_surfaces()
	data.commit_to_surface(array_plane)
	
	mesh_instance_3d.mesh = array_plane
