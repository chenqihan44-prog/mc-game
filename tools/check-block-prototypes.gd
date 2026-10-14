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

func run_checks():
	var checks: Array = []
	var revision_two = "--v2" in OS.get_cmdline_user_args()
	var expected_counts = {"cow":2372,"pig":1832,"guardian":5748} if revision_two else {"cow":252,"pig":228,"guardian":348}
	for id in ["cow", "pig", "guardian"]:
		var packed = load("res://models/" + id + ".glb") as PackedScene
		assert(packed != null, "GLB scene import failed: " + id)
		var model = packed.instantiate()
		root.add_child(model)
		await process_frame
		var skeleton = find_type(model, "Skeleton3D") as Skeleton3D
		var mesh = find_type(model, "MeshInstance3D") as MeshInstance3D
		var player = find_type(model, "AnimationPlayer") as AnimationPlayer
		assert(skeleton != null and mesh != null and player != null, "Missing rig or animations")
		assert(mesh.skin != null and mesh.mesh.get_surface_count() == 1, "Skin or material count")
		var mat = mesh.get_active_material(0) as StandardMaterial3D
		assert(mat != null and mat.albedo_texture != null)
		assert(mat.texture_filter == BaseMaterial3D.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS or mat.texture_filter == BaseMaterial3D.TEXTURE_FILTER_NEAREST, "Pixel sampler not preserved")
		var triangles = 0
		for surface in mesh.mesh.get_surface_count():
			var arrays = mesh.mesh.surface_get_arrays(surface)
			triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
		var expected = expected_counts[id]
		assert(triangles == expected, "Unexpected triangle count")
		var clips = ["idle", "walk", "run", "slam" if id == "guardian" else "graze"]
		for clip in clips:
			assert(player.has_animation(clip), "Missing animation: " + clip)
		var bone = skeleton.find_bone("LegL" if id == "guardian" else "LegFrontL")
		player.play("walk")
		player.seek(0, true)
		player.advance(0)
		var first = skeleton.get_bone_pose_rotation(bone)
		player.seek(.25, true)
		player.advance(0)
		var second = skeleton.get_bone_pose_rotation(bone)
		assert(abs(first.dot(second)) < .9999, "Walk clip doesn't move leg bones")
		if revision_two and id == "guardian":
			assert(mat.emission_enabled and mat.emission_texture != null, "Core emission mask must survive import")
		checks.append({"id": id, "triangles": triangles, "bones": skeleton.get_bone_count(), "clips": clips, "animationMovesLeg": true, "pixelSamplingPreserved": true})
		model.queue_free()
		await process_frame
	var output_dir = "block-creature-prototypes-v2-2026-10-13" if revision_two else "block-creature-prototypes-2026-10-13"
	var output = FileAccess.open("F:/我的世界/docs/previews/" + output_dir + "/godot-validation.json", FileAccess.WRITE)
	output.store_string(JSON.stringify({"passed": true, "engine": Engine.get_version_info().string, "checks": checks, "scope": "isolated GLB import and playback; production game unchanged"}, "  "))
	print("BLOCK_PROTOTYPES_GODOT_OK " + JSON.stringify(checks))
	quit(0)
