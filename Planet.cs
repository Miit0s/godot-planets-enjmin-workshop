using Godot;
using System;

public partial class Planet : Node3D
{
	[Export] public float RotationSpeed = 0.5f;
	
	public override void _Ready()
	{
		GD.Print("Planet initialized");
	}

	public override void _Process(double delta)
	{
		RotateY(RotationSpeed * (float)delta);
	}
}

