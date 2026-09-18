using Godot; //AI CODE

// Orbit camera in the usual 3D-platformer shape: a pivot that chases the //AI CODE
// player, a mouse-driven yaw/pitch, and a spring arm so walls push the //AI CODE
// camera in instead of clipping through. //AI CODE
public partial class CameraRig : Node3D //AI CODE
{ //AI CODE
	[Export] public Node3D Target; //AI CODE
	[Export] public float Distance = 8f; //AI CODE
	[Export] public float Height = 1.5f; //AI CODE
	[Export] public float FollowSpeed = 12f; //AI CODE
	[Export] public float MouseSensitivity = 0.004f; //AI CODE
	[Export] public float MinPitchDegrees = -65f; //AI CODE
	[Export] public float MaxPitchDegrees = 35f; //AI CODE

	private SpringArm3D _arm; //AI CODE
	private float _yaw; //AI CODE
	private float _pitch = Mathf.DegToRad(-15f); //AI CODE

	public override void _Ready() //AI CODE
	{ //AI CODE
		// Live under the player but ignore its spin: a rolling parent would //AI CODE
		// otherwise tumble the whole view. //AI CODE
		TopLevel = true; //AI CODE
		_arm = GetNode<SpringArm3D>("SpringArm3D"); //AI CODE
		_arm.SpringLength = Distance; //AI CODE

		Target ??= FindTarget(GetTree().Root); //AI CODE
		if (Target is CollisionObject3D body) //AI CODE
		{ //AI CODE
			// The pivot sits inside the player, so the arm would otherwise //AI CODE
			// collide with it and slam the camera to zero distance. //AI CODE
			_arm.AddExcludedObject(body.GetRid()); //AI CODE
		} //AI CODE

		if (Target != null) //AI CODE
		{ //AI CODE
			GlobalPosition = Target.GlobalPosition + Vector3.Up * Height; //AI CODE
		} //AI CODE

		Input.MouseMode = Input.MouseModeEnum.Captured; //AI CODE
	} //AI CODE

	private static Node3D FindTarget(Node node) //AI CODE
	{ //AI CODE
		if (node is Player player) //AI CODE
		{ //AI CODE
			return player; //AI CODE
		} //AI CODE
		foreach (Node child in node.GetChildren()) //AI CODE
		{ //AI CODE
			Node3D found = FindTarget(child); //AI CODE
			if (found != null) //AI CODE
			{ //AI CODE
				return found; //AI CODE
			} //AI CODE
		} //AI CODE
		return null; //AI CODE
	} //AI CODE

	public override void _UnhandledInput(InputEvent @event) //AI CODE
	{ //AI CODE
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured) //AI CODE
		{ //AI CODE
			_yaw -= motion.Relative.X * MouseSensitivity; //AI CODE
			_pitch = Mathf.Clamp( //AI CODE
				_pitch - motion.Relative.Y * MouseSensitivity, //AI CODE
				Mathf.DegToRad(MinPitchDegrees), //AI CODE
				Mathf.DegToRad(MaxPitchDegrees)); //AI CODE
		} //AI CODE

		// Escape releases the cursor; clicking back in recaptures it. //AI CODE
		if (@event.IsActionPressed("ui_cancel")) //AI CODE
		{ //AI CODE
			Input.MouseMode = Input.MouseModeEnum.Visible; //AI CODE
		} //AI CODE
		else if (@event is InputEventMouseButton { Pressed: true }) //AI CODE
		{ //AI CODE
			Input.MouseMode = Input.MouseModeEnum.Captured; //AI CODE
		} //AI CODE
	} //AI CODE

	// Physics tick, so the camera samples the body after it has moved. //AI CODE
	public override void _PhysicsProcess(double delta) //AI CODE
	{ //AI CODE
		if (Target == null) //AI CODE
		{ //AI CODE
			return; //AI CODE
		} //AI CODE

		// Chase the player rather than hard-locking: a rolling body jitters, //AI CODE
		// and the lag smooths that out of the view. //AI CODE
		Vector3 goal = Target.GlobalPosition + Vector3.Up * Height; //AI CODE
		GlobalPosition = GlobalPosition.Lerp(goal, Mathf.Min(1f, (float)delta * FollowSpeed)); //AI CODE

		_arm.SpringLength = Distance; //AI CODE
		Rotation = new Vector3(_pitch, _yaw, 0f); //AI CODE
	} //AI CODE
} //AI CODE
