using Godot;
using System;

public partial class Camera : Node3D
{
	[Export] public float MoveSpeed = 10.0f;
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float MinPitch = -89.0f;
	[Export] public float MaxPitch = 89.0f;
	
	private Camera3D _camera;
	private float _pitch = 0.0f;
	private float _yaw = 0.0f;
	
	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
		
		Input.MouseMode = Input.MouseModeEnum.Captured;
		
		GD.Print("Camera initialized with WASD + Mouse controls");
		GD.Print("Press ESC to release mouse");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
			else
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
		}
		
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yaw -= mouseMotion.Relative.X * MouseSensitivity;
			_pitch -= mouseMotion.Relative.Y * MouseSensitivity;
			
			_pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
			
			Rotation = new Vector3(_pitch, _yaw, 0);
		}
	}

	public override void _Process(double delta)
	{
		Vector3 velocity = Vector3.Zero;
		
		if (Input.IsKeyPressed(Key.W))
			velocity -= Transform.Basis.Z;
		if (Input.IsKeyPressed(Key.S))
			velocity += Transform.Basis.Z;
		if (Input.IsKeyPressed(Key.A))
			velocity -= Transform.Basis.X;
		if (Input.IsKeyPressed(Key.D))
			velocity += Transform.Basis.X;
		if (Input.IsKeyPressed(Key.Space))
			velocity += Transform.Basis.Y;
		if (Input.IsKeyPressed(Key.Shift))
			velocity -= Transform.Basis.Y;
		
		if (velocity.Length() > 0)
		{
			velocity = velocity.Normalized();
		}
		
		Position += velocity * MoveSpeed * (float)delta;
	}
}
