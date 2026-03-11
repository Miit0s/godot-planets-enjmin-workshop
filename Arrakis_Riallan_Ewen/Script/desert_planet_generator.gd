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

#How deep is the rock sunk into the desert
@export var base_depth: float = 2.0
#Min value the noise should have to trigger a rock
@export var rock_threshold: float = 0.5

@export var min_neighbors_for_terrace: int = 4
@export var max_neighbors_for_terrace: int = 16

@export var min_height_resize_for_terrace: float = 1.0
@export var max_height_resize_for_terrace: float = 2.0

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
	
	var processed_vertices: Array[bool] = []
	processed_vertices.resize(data.get_vertex_count())
	processed_vertices.fill(false)
	
	for i in range(data.get_vertex_count()):
		if processed_vertices[i]: continue
		
		var vertex: Vector3 = data.get_vertex(i)
		
		var normalized_noise: float = rock_noise.get_noise_3dv(vertex)
		normalized_noise = (normalized_noise + 1.0) / 2.0
		
		if normalized_noise < rock_threshold:
			apply_height_to_vertex(data, i, 0.0)
			processed_vertices[i] = true
		else:
			var number_of_vertex_for_terrace: int = randi_range(min_neighbors_for_terrace, max_neighbors_for_terrace)
			var random_height: float = normalized_noise * randf_range(min_height_resize_for_terrace, max_height_resize_for_terrace)
			create_terrace(data, i, random_height, number_of_vertex_for_terrace, processed_vertices)
	
	array_mesh.clear_surfaces()
	data.commit_to_surface(array_mesh)
	
	_surface_tool.clear()
	_surface_tool.create_from(array_mesh, 0)
	_surface_tool.generate_normals()
	
	rock_mesh_instance_3d.mesh = _surface_tool.commit()

func get_vertex_neighbors(data: MeshDataTool, vertex_index: int) -> Array[int]:
	var neighbors: Array[int] = []
	
	var edges: PackedInt32Array = data.get_vertex_edges(vertex_index)
	
	for edge_index in edges:
		var v1: int = data.get_edge_vertex(edge_index, 0)
		var v2: int = data.get_edge_vertex(edge_index, 1)
		
		var vertex_to_append: int = v1 if v2 == vertex_index else v2
		neighbors.append(vertex_to_append)
	
	return neighbors

func create_terrace(data: MeshDataTool, vertex_index: int, normalized_height: float, terrace_size: int, processed_vertices: Array[bool]):
	var size_to_proceed: int = terrace_size
	var possible_index_to_use: Array[int] = [vertex_index]
	
	apply_height_to_vertex(data, vertex_index, normalized_height)
	processed_vertices[vertex_index] = true
	
	while size_to_proceed > 0:
		if possible_index_to_use.is_empty(): break
		
		var random_vertex_index: int = possible_index_to_use.pop_at(randi_range(0, possible_index_to_use.size() - 1))
		var vertex_neighbors: Array[int] = get_vertex_neighbors(data, random_vertex_index)
		
		for neighbor in vertex_neighbors:
			if size_to_proceed <= 0: break
			if processed_vertices[neighbor]: continue
			
			apply_height_to_vertex(data, neighbor, normalized_height)
			processed_vertices[neighbor] = true
			possible_index_to_use.append(neighbor)
			size_to_proceed -= 1

func apply_height_to_vertex(data: MeshDataTool, vertex_index: int, normalized_noise: float):
	var vertex: Vector3 = data.get_vertex(vertex_index)
	var normal: Vector3 = vertex.normalized()
	
	var current_height: float = (planet_radius - base_depth) + (normalized_noise * noise_height_multiplication_for_rock)
	vertex = normal * current_height
	data.set_vertex(vertex_index, vertex)
