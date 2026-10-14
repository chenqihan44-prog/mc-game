extends SceneTree

func _initialize():
	call_deferred("run_checks")

func find_type(node: Node, type_name: String) -> Node:
	if node.is_class(type_name):
		return node
	for child in node.get_children():
		var found = find_type(child, type_name)
		if found != null:
			return found
	return null

func pose_differs(skeleton: Skeleton3D, rotations: Array, positions: Array) -> bool:
	for n in skeleton.get_bone_count():
		if abs(rotations[n].dot(skeleton.get_bone_pose_rotation(n))) < .999999:
			return true
		if positions[n].distance_to(skeleton.get_bone_pose_position(n)) > .00001:
			return true
	return false

func run_checks():
	var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://models/manifest.json"))
	assert(manifest.size() == 26, "Expected complete 26-model catalog")
	var checks: Array = []
	for item in manifest:
		var id: String = item.id
		var packed = load("res://models/" + id + ".glb") as PackedScene
		assert(packed != null, "Import failed: " + id)
		var model = packed.instantiate()
		root.add_child(model)
		await process_frame
		var skeleton = find_type(model, "Skeleton3D") as Skeleton3D
		var mesh = find_type(model, "MeshInstance3D") as MeshInstance3D
		var player = find_type(model, "AnimationPlayer") as AnimationPlayer
		assert(skeleton != null and mesh != null and player != null, "Rig components: " + id)
		assert(mesh.skin != null and mesh.mesh.get_surface_count() == 1, "Skin/material: " + id)
		var mat = mesh.get_active_material(0) as StandardMaterial3D
		assert(mat != null and mat.albedo_texture != null, "Material: " + id)
		assert(mat.texture_filter == BaseMaterial3D.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS or mat.texture_filter == BaseMaterial3D.TEXTURE_FILTER_NEAREST, "Pixel sampling: " + id)
		assert(mat.emission_enabled == bool(item.emissionMask), "Emission settings: " + id)
		if item.emissionMask:
			assert(mat.emission_texture != null, "Emission mask: " + id)
		var triangles = 0
		for surface in mesh.mesh.get_surface_count():
			var arrays = mesh.mesh.surface_get_arrays(surface)
			triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
		assert(triangles == int(item.triangles), "Triangle count: " + id)
		var clips: Array = []
		for clip in item.clips:
			assert(player.has_animation(clip), "Missing animation: " + id + "/" + clip)
			player.play(clip)
			player.seek(0, true)
			player.advance(0)
			var rotations: Array = []
			var positions: Array = []
			for n in skeleton.get_bone_count():
				rotations.append(skeleton.get_bone_pose_rotation(n))
				positions.append(skeleton.get_bone_pose_position(n))
			var animation = player.get_animation(clip)
			player.seek(animation.length * .25, true)
			player.advance(0)
			assert(pose_differs(skeleton, rotations, positions), "Animation doesn't affect skeleton: " + id + "/" + clip)
			clips.append(clip)
		var leg_count = 0
		for n in skeleton.get_bone_count():
			if skeleton.get_bone_name(n).begins_with("Leg"):
				leg_count += 1
		if item.family == "arthropod":
			assert(leg_count == 8, "Eight spider legs required: " + id)
		elif item.family == "bird":
			assert(leg_count == 2, "Two bird legs required")
		elif item.family == "quadruped":
			assert(leg_count == 4, "Four quadruped legs required: " + id)
		checks.append({"id": id, "triangles": triangles, "bones": skeleton.get_bone_count(), "clips": clips, "allClipsMoveSkeleton": true, "pixelSampling": true, "emissionMask": mat.emission_enabled})
		print("MODEL_CHECK_OK " + id)
		model.queue_free()
		await process_frame
	var output = FileAccess.open("F:/我的世界/docs/previews/block-creature-collection-2026-10-13/godot-validation.json", FileAccess.WRITE)
	output.store_string(JSON.stringify({"passed": true, "models": 26, "engine": Engine.get_version_info().string, "scope": "isolated import, triangle/material/emission integrity, every animation clip, limb counts; production game unchanged", "checks": checks}, "  "))
	print("ALL_BLOCK_CREATURES_GODOT_OK")
	quit(0)
