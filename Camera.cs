using Godot;
using System;

public enum CameraMode
{
	Fly,
	Walk
}

public partial class Camera : CharacterBody3D
{
	[Export] public float MoveSpeed = 10.0f;
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float MinPitch = -89.0f;
	[Export] public float MaxPitch = 89.0f;
	[Export] public float JumpVelocity = 5.0f;
	[Export] public float AlignmentSpeed = 5.0f;
	[Export] public float MinGravityForAlignment = 0.1f;

	private Camera3D _camera;
	private float _pitch = 0.0f;
	private float _yaw = 0.0f;
	private CameraMode _mode = CameraMode.Fly;
	private Vector3 _velocity = Vector3.Zero;
	private Godot.Collections.Array<Planet> _planets = new Godot.Collections.Array<Planet>();
	
	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");

		// Find all Planet nodes in the scene
		FindAllPlanets();

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void FindAllPlanets()
	{
		_planets.Clear();
		FindPlanetsRecursive(GetTree().Root);
		GD.Print($"Found {_planets.Count} planet(s) in the scene");
	}

	private void FindPlanetsRecursive(Node node)
	{
		// TODO: GetChildren can be recursive 
		if (node is Planet planet && node != this)
		{
			_planets.Add(planet);
		}

		foreach (Node child in node.GetChildren())
		{
			FindPlanetsRecursive(child);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.Escape)
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
			else if (keyEvent.Keycode == Key.Tab)
			{
				// Toggle between Fly and Walk modes
				_mode = (_mode == CameraMode.Fly) ? CameraMode.Walk : CameraMode.Fly;
				GD.Print($"Camera mode: {_mode}");
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

	public override void _PhysicsProcess(double delta)
	{
		if (_mode == CameraMode.Fly)
		{
			ProcessFlyMode(delta);
		}
		else
		{
			ProcessWalkMode(delta);
		}
	}

	private void ProcessFlyMode(double delta)
	{
		Vector3 velocity = Vector3.Zero;

		if (Input.IsActionPressed("move_forward"))
			velocity -= Transform.Basis.Z;
		if (Input.IsActionPressed("move_backward"))
			velocity += Transform.Basis.Z;
		if (Input.IsActionPressed("move_left"))
			velocity -= Transform.Basis.X;
		if (Input.IsActionPressed("move_right"))
			velocity += Transform.Basis.X;
		if (Input.IsActionPressed("move_up"))
			velocity += Transform.Basis.Y;
		if (Input.IsActionPressed("move_down"))
			velocity -= Transform.Basis.Y;

		if (velocity.Length() > 0)
		{
			velocity = velocity.Normalized();
		}

		Velocity = velocity * MoveSpeed;
		MoveAndSlide();
	}

	private void ProcessWalkMode(double delta)
	{
		if (_planets.Count == 0)
		{
			GD.PrintErr("No planets found for Walk mode!");
			ProcessFlyMode(delta);
			return;
		}

		// Sum gravity forces from all planets
		Vector3 totalGravity = Vector3.Zero;
		foreach (Planet planet in _planets)
		{
			totalGravity += planet.GetForce(GlobalPosition);
		}

		float gravityMagnitude = totalGravity.Length();
		Vector3 upDirection = gravityMagnitude > 0.001f ? -totalGravity.Normalized() : Vector3.Up;

		// Apply gravity to velocity
		_velocity += totalGravity * (float)delta;

		// Get input direction relative to camera orientation
		Vector3 inputDir = Vector3.Zero;
		if (Input.IsActionPressed("move_forward"))
			inputDir -= Transform.Basis.Z;
		if (Input.IsActionPressed("move_backward"))
			inputDir += Transform.Basis.Z;
		if (Input.IsActionPressed("move_left"))
			inputDir -= Transform.Basis.X;
		if (Input.IsActionPressed("move_right"))
			inputDir += Transform.Basis.X;

		// Project input direction onto the plane perpendicular to gravity
		if (inputDir.Length() > 0)
		{
			inputDir = inputDir.Normalized();
			inputDir = (inputDir - inputDir.Dot(upDirection) * upDirection).Normalized();
			_velocity += inputDir * MoveSpeed * (float)delta * 10.0f; // Acceleration factor
		}

		// Apply friction on the horizontal plane
		Vector3 horizontalVelocity = _velocity - _velocity.Dot(upDirection) * upDirection;
		horizontalVelocity *= 0.9f; // TODO: framerate intedependent
		_velocity = horizontalVelocity + _velocity.Dot(upDirection) * upDirection;

		// Jump
		if (Input.IsActionJustPressed("move_up") && IsOnFloor())
		{
			_velocity += upDirection * JumpVelocity;
		}

		// Set the up direction for MoveAndSlide to use for floor detection
		UpDirection = upDirection;

		// Apply velocity using MoveAndSlide with proper floor settings
		Velocity = _velocity;

		// Configure floor behavior
		FloorStopOnSlope = true;
		FloorMaxAngle = Mathf.DegToRad(45.0f);

		MoveAndSlide();

		// Get velocity back after slide with collision response
		_velocity = Velocity;

		// Orient camera to align with gravity (proportional to gravity strength)
		AlignToGravity(upDirection, gravityMagnitude, (float)delta);
	}

	private void AlignToGravity(Vector3 upDirection, float gravityMagnitude, float delta)
	{
		return;
		// Only rotate if gravity is significant
		if (gravityMagnitude < MinGravityForAlignment)
		{
			// In zero gravity, just use standard rotation
			Rotation = new Vector3(_pitch, _yaw, 0);
			return;
		}

		// Calculate rotation speed based on gravity strength (normalized)
		float rotationFactor = Mathf.Min(gravityMagnitude / 9.8f, 1.0f);
		float effectiveAlignmentSpeed = AlignmentSpeed * rotationFactor * delta;

		// Get current transform
		Quaternion currentRotation = GlobalTransform.Basis.GetRotationQuaternion();
		Vector3 currentUp = GlobalTransform.Basis.Y;

		// Calculate target up direction
		Vector3 targetUp = upDirection;

		// Use quaternion rotation to smoothly align current up to target up
		Quaternion alignmentRotation = Quaternion.Identity;

		float dot = currentUp.Dot(targetUp);
		if (dot < 0.9999f && dot > -0.9999f) // Avoid gimbal lock
		{
			Vector3 axis = currentUp.Cross(targetUp).Normalized();
			float angle = Mathf.Acos(Mathf.Clamp(dot, -1.0f, 1.0f));

			// Limit rotation speed
			float maxAngle = effectiveAlignmentSpeed;
			angle = Mathf.Min(angle, maxAngle);

			alignmentRotation = new Quaternion(axis, angle);
		}

		// Apply alignment rotation to current rotation
		Quaternion newRotation = alignmentRotation * currentRotation;

		// Convert to basis
		Basis alignedBasis = new Basis(newRotation);

		// Now apply camera look rotation (pitch and yaw)
		// Get the new up vector after alignment
		Vector3 newUp = alignedBasis.Y;

		// Get right vector for pitch rotation
		Vector3 right = alignedBasis.X;

		// Apply pitch around the right axis
		Quaternion pitchQuat = new Quaternion(right, _pitch);

		// Apply yaw around the up axis
		Quaternion yawQuat = new Quaternion(newUp, _yaw);

		// Combine: first alignment, then yaw, then pitch
		Quaternion finalRotation = yawQuat * newRotation * pitchQuat;

		// Set the final transform
		GlobalTransform = new Transform3D(new Basis(finalRotation), GlobalPosition);
	}
}
