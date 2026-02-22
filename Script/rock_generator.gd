@tool
extends Node
class_name RockGenerator

@export_category("Mesh Parameters")
@export var size_depth: int = 50
@export var size_width: int = 50
@export var mesh_resolution: int = 3

@export_category("Rock Parameters")
@export var noise_height_multiplication: float = 30.0
@export var base_depth: float = 2.0
@export var rock_threshold: float = 0.2
@export var steepness: float = 5.0

@export var terrace_steps: int = 8
@export var terrace_strength: float = 0.85

@export var noise: FastNoiseLite
@export var material: StandardMaterial3D

@export_category("Ref")
@export var mesh_instance_3d: MeshInstance3D

@export_tool_button("Generate New Map") var generate_action = generate

var _surface_tool: SurfaceTool

func _ready() -> void:
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
	var array_mesh = _surface_tool.commit()
	data.create_from_surface(array_mesh, 0)
	
	for i in range(data.get_vertex_count()):
		var vertex: Vector3 = data.get_vertex(i)
		
		var normalized_noise: float = noise.get_noise_2d(vertex.x, vertex.z)
		normalized_noise = (normalized_noise + 1.0) / 2.0
		
		if normalized_noise < rock_threshold:
			normalized_noise = 0.0
		else:
			normalized_noise = (normalized_noise - rock_threshold) / (1.0 - rock_threshold)
			normalized_noise = pow(normalized_noise, steepness)
			
			var terraced_normalized: float = floor(normalized_noise * terrace_steps) / float(terrace_steps)
			normalized_noise = lerp(normalized_noise, terraced_normalized, terrace_strength)
		
		vertex.y = (normalized_noise * noise_height_multiplication) - base_depth
		data.set_vertex(i, vertex)
	
	array_mesh.clear_surfaces()
	data.commit_to_surface(array_mesh)
	
	_surface_tool.clear()
	_surface_tool.create_from(array_mesh, 0)
	_surface_tool.generate_normals()
	
	mesh_instance_3d.mesh = _surface_tool.commit()
