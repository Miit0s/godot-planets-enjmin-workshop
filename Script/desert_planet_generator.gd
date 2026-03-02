@tool
extends  Node3D
class_name DesertPlanetGenerator

@export var planet_radius: float = 50

@export_category("Desert")
@export var desert_mesh_resolution: int = 5
@export var noise_height_multiplication_for_desert: float = 2

@export var desert_mesh_instance_3d: MeshInstance3D
@export var sand_noise: FastNoiseLite
@export var sand_material: StandardMaterial3D

@export_category("Rock")
@export var rocks_mesh_resolution: int = 5
@export var noise_height_multiplication_for_rock: float = 2

@export var base_depth: float = 2.0
@export var rock_threshold: float = 0.2
@export var steepness: float = 5.0

@export var terrace_steps: int = 8
@export var terrace_strength: float = 0.85

@export var rock_mesh_instance_3d: MeshInstance3D
@export var rock_noise: FastNoiseLite
@export var rock_material: StandardMaterial3D

@export_tool_button("Generate New Map") var generate_action = generate

var _surface_tool: SurfaceTool

func _ready():
	generate()

func create_new_mesh(mesh_instance: MeshInstance3D, material: Material, mesh_resolution: int, radius: float):
	var sphere_mesh: SphereMesh = SphereMesh.new()
	
	sphere_mesh.radius = radius
	sphere_mesh.height = radius * 2.0
	sphere_mesh.radial_segments = mesh_resolution * 2
	sphere_mesh.rings = mesh_resolution
	sphere_mesh.material = material
	
	_surface_tool = SurfaceTool.new()
	_surface_tool.create_from(sphere_mesh, 0)
	
	mesh_instance.mesh = _surface_tool.commit()

func generate():
	generate_desert()
	generate_rocks()

func generate_desert():
	create_new_mesh(desert_mesh_instance_3d, sand_material, desert_mesh_resolution, planet_radius)
	
	sand_noise.seed = randi()
	
	var data: MeshDataTool = MeshDataTool.new()
	var array_mesh = _surface_tool.commit()
	data.create_from_surface(array_mesh, 0)
	
	for i in range(data.get_vertex_count()):
		var vertex: Vector3 = data.get_vertex(i)
		var noise_value: float = sand_noise.get_noise_3dv(vertex)
		var normal: Vector3 = vertex.normalized()
		
		vertex += normal * noise_value * noise_height_multiplication_for_desert
		data.set_vertex(i, vertex)
	
	array_mesh.clear_surfaces()
	data.commit_to_surface(array_mesh)
	
	_surface_tool.clear()
	_surface_tool.create_from(array_mesh, 0)
	_surface_tool.generate_normals()
	
	desert_mesh_instance_3d.mesh = _surface_tool.commit()

func generate_rocks():
	create_new_mesh(rock_mesh_instance_3d, rock_material, rocks_mesh_resolution, planet_radius - base_depth)
	
	rock_noise.seed = randi()
	
	var data: MeshDataTool = MeshDataTool.new()
	var array_mesh = _surface_tool.commit()
	data.create_from_surface(array_mesh, 0)
	
	for i in range(data.get_vertex_count()):
		var vertex: Vector3 = data.get_vertex(i)
		
		var normalized_noise: float = rock_noise.get_noise_3dv(vertex)
		normalized_noise = (normalized_noise + 1.0) / 2.0
		var normal: Vector3 = vertex.normalized()
		
		if normalized_noise < rock_threshold:
			normalized_noise = 0.0
		else:
			normalized_noise = (normalized_noise - rock_threshold) / (1.0 - rock_threshold)
			normalized_noise = pow(normalized_noise, steepness)
			
			var terraced_normalized: float = floor(normalized_noise * terrace_steps) / float(terrace_steps)
			normalized_noise = lerp(normalized_noise, terraced_normalized, terrace_strength)
		
		var current_height: float = planet_radius - base_depth + (normalized_noise * noise_height_multiplication_for_rock)
		vertex = normal * current_height
		data.set_vertex(i, vertex)
	
	array_mesh.clear_surfaces()
	data.commit_to_surface(array_mesh)
	
	_surface_tool.clear()
	_surface_tool.create_from(array_mesh, 0)
	_surface_tool.generate_normals()
	
	rock_mesh_instance_3d.mesh = _surface_tool.commit()
