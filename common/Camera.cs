using Godot;
using System;

public enum CameraMode
{
	Fly,
	Walk
}

public partial class Camera : CharacterBody3D
{
	[Export] public float FlySpeed = 100.0f;
	[Export] public float WalkSpeed = 20.0f;
	[Export] public float FlyFriction = 0.02f;
	[Export] public float WalkFriction = 0.03f;

	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float MinPitch = -89.0f;
	[Export] public float MaxPitch = 89.0f;
	[Export] public float JumpVelocity = 5.0f;
	[Export] public float AlignmentSpeed = 2.0f;
	[Export] public float MinGravityForAlignment = 0.1f;

	private Camera3D camera;
	
	private float pitch = 0.0f;
	private float yaw = 0.0f;
	
	private CameraMode mode = CameraMode.Fly;
	private Vector3 velocity = Vector3.Zero;
	private Godot.Collections.Array<Planet> planets = [];
	
	// For walk mode: cached gravity-aligned basis
	private Basis gravityAlignedBasis = Basis.Identity;
	
	// For fly mode: independent basis that rotates freely
	private Basis flyBasis = Basis.Identity;
	
	public override void _Ready()
	{
		camera = GetNode<Camera3D>("Camera3D");
		FindAllPlanets();
		Input.MouseMode = Input.MouseModeEnum.Captured;
		
		// Initialize fly basis from current transform
		flyBasis = Transform.Basis.Orthonormalized();
	}

	private void FindAllPlanets()
	{
		planets.Clear();
		FindPlanetsRecursive(GetTree().Root);
	}

	private void FindPlanetsRecursive(Node node)
	{
		if (node is Planet planet && node != this)
		{
			planets.Add(planet);
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
				Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured 
					? Input.MouseModeEnum.Visible 
					: Input.MouseModeEnum.Captured;
			}
			else if (keyEvent.Keycode == Key.Tab)
			{
				if (mode == CameraMode.Fly)
				{
					// Switching to Walk: sync gravity basis from current fly basis
					mode = CameraMode.Walk;
					gravityAlignedBasis = flyBasis;
					pitch = 0.0f;
					yaw = 0.0f;
				}
				else
				{
					// Switching to Fly: capture current transform as fly basis
					mode = CameraMode.Fly;
					flyBasis = Transform.Basis.Orthonormalized();
				}
			}
		}
		
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			if (mode == CameraMode.Fly)
			{
				// In fly mode, apply rotations incrementally to avoid gimbal lock
				float deltaYaw = -mouseMotion.Relative.X * MouseSensitivity;
				float deltaPitch = -mouseMotion.Relative.Y * MouseSensitivity;
				
				// Apply yaw around current local Y axis
				Quaternion yawRot = new Quaternion(flyBasis.Y, deltaYaw);
				flyBasis = new Basis(yawRot) * flyBasis;
				
				// Apply pitch around the NEW local X axis (after yaw)
				Quaternion pitchRot = new Quaternion(flyBasis.X, deltaPitch);
				flyBasis = new Basis(pitchRot) * flyBasis;
				
				// Orthonormalize to prevent drift accumulation
				flyBasis = flyBasis.Orthonormalized();
			}
			else
			{
				// In walk mode, accumulate pitch/yaw for gravity-relative look
				yaw -= mouseMotion.Relative.X * MouseSensitivity;
				pitch -= mouseMotion.Relative.Y * MouseSensitivity;
				pitch = Mathf.Clamp(pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateMovement(delta);
	}

	private void UpdateMovement(double delta)
	{
		Vector3 totalGravity = Vector3.Zero;
		foreach (Planet planet in planets)
		{
			totalGravity += planet.GetForce(GlobalPosition);
		}

		float gravityMagnitude = totalGravity.Length();
		Vector3 upDirection = gravityMagnitude > 0.001f ? -totalGravity.Normalized() : Vector3.Up;


		Basis finalBasis;
		if(mode == CameraMode.Walk) {
			UpdateGravityAlignment(totalGravity, (float)delta);
			velocity += totalGravity * (float)delta;
			finalBasis = ApplyMouseLookToGravityBasis();
		}
		else {
			// Fly mode: use the fly basis directly
			finalBasis = flyBasis;
		}
		Vector3 inputDir = Vector3.Zero;
		if (Input.IsActionPressed("move_forward"))
			inputDir -= finalBasis.Z;
		if (Input.IsActionPressed("move_backward"))
			inputDir += finalBasis.Z;
		if (Input.IsActionPressed("move_left"))
			inputDir -= finalBasis.X;
		if (Input.IsActionPressed("move_right"))
			inputDir += finalBasis.X;
		
		if(mode == CameraMode.Fly)
        {
            if(Input.IsActionPressed("move_up")) {
				inputDir += finalBasis.Y;
			}
			if(Input.IsActionPressed("move_down")) {
				inputDir -= finalBasis.Y;
			}
        }
		else
        {
            if (Input.IsActionJustPressed("move_up") && IsOnFloor())
			{
				velocity += upDirection * JumpVelocity;
			}
        }

		if (inputDir.Length() > 0)
		{
			var speed = mode == CameraMode.Walk ? WalkSpeed : FlySpeed;
			inputDir = inputDir.Normalized();
			
			// In Walk mode, project input direction onto the plane perpendicular to gravity
			// In Fly mode, move freely in the camera's direction
			if (mode == CameraMode.Walk)
			{
				inputDir = (inputDir - inputDir.Dot(upDirection) * upDirection).Normalized();
			}
			
			velocity += inputDir * speed * (float)delta;
		}

		if(mode == CameraMode.Walk) {
			// Apply friction on the horizontal plane
			Vector3 horizontalVelocity = velocity - velocity.Dot(upDirection) * upDirection;
			horizontalVelocity *= Mathf.Pow(WalkFriction, (float)delta); // Frame-rate independent
			velocity = horizontalVelocity + velocity.Dot(upDirection) * upDirection;
		}
		else {
			velocity *= Mathf.Pow(FlyFriction, (float)delta);
		}

		UpDirection = upDirection;
		Velocity = velocity;
		// FloorStopOnSlope = true;
		FloorMaxAngle = Mathf.DegToRad(45.0f);

		MoveAndSlide();

		velocity = Velocity;
		Transform = new Transform3D(finalBasis, Transform.Origin);
	}

	private void UpdateGravityAlignment(Vector3 gravity, float dt)
	{
		if(gravity.Length() < 0.001f)
			return;
		gravity = -gravity.Normalized();
		Vector3 currentUp = gravityAlignedBasis.Y.Normalized();
				
		Vector3 axis = currentUp.Cross(gravity);
		if (axis.Length() < 0.01f)
			return;
		axis = axis.Normalized();
		float angle = currentUp.AngleTo(gravity);
		var rotation = new Quaternion(axis, angle * dt * AlignmentSpeed);
		gravityAlignedBasis = new Basis(rotation) * gravityAlignedBasis;
		gravityAlignedBasis = gravityAlignedBasis.Orthonormalized();
	}

	private Basis ApplyMouseLookToGravityBasis()
	{
		// Extract axes from gravity-aligned basis
		Vector3 up = gravityAlignedBasis.Y.Normalized();
		Vector3 right = gravityAlignedBasis.X.Normalized();
		Vector3 forward = gravityAlignedBasis.Z.Normalized();
		
		// Safety check: ensure basis is valid
		if (up.LengthSquared() < 0.0001f || right.LengthSquared() < 0.0001f)
		{
			GD.PushWarning("Invalid gravity-aligned basis, resetting");
			gravityAlignedBasis = Basis.Identity;
			up = Vector3.Up;
			right = Vector3.Right;
			forward = Vector3.Forward;
		}
		
		// Apply yaw around the up axis
		Quaternion yawRotation = new Quaternion(up, yaw);
		
		// After yaw, calculate the new right axis for pitch
		Vector3 rotatedRight = (yawRotation * right).Normalized();
		
		// Safety check: ensure rotatedRight is normalized
		if (rotatedRight.LengthSquared() < 0.0001f)
		{
			rotatedRight = right; // Fallback to original right
		}
		
		// Apply pitch around the rotated right axis
		Quaternion pitchRotation = new Quaternion(rotatedRight, pitch);
		
		// Combine: pitch * yaw * gravityAligned
		Quaternion finalRotation = pitchRotation * yawRotation;
		Basis finalBasis = new Basis(finalRotation) * gravityAlignedBasis;
		
		return finalBasis.Orthonormalized();
	}
}
