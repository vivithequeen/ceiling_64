using Godot;
using System;

public partial class Player : RigidBody3D
{
	private const int MaxContacts = 16;

	[Export] public float TorqueStrength = 7f;
	[Export] public float MaxAngularSpeed = 20f;
	[Export] public float MaxAirAngularSpeed = 6f;
	[Export] public float AirControl = 0.2f;
	[Export] public float MaxAirSpeed = 1f;
	[Export] public float BrakeStrength = 8f;

	private MeshInstance3D _comMarker;
	private MeshInstance3D[] _contactMarkers;

	// World-space collision points from the last physics step, valid up to ContactCount.
	public Vector3[] ContactPoints { get; } = new Vector3[MaxContacts];
	public Vector3[] ContactNormals { get; } = new Vector3[MaxContacts];
	public int ContactCount { get; private set; }

    public float bouncedDebounce = 3;


	[Export] Camera3D camera;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = MaxContacts;

        // Simple debug marker showing where the center of mass is pushed to.
        _comMarker = CreateMarker(Colors.Red, 0.15f);

        // One reusable marker per possible contact point.
        _contactMarkers = new MeshInstance3D[MaxContacts];
        for (int i = 0; i < MaxContacts; i++)
        {
            _contactMarkers[i] = CreateMarker(Colors.Lime, 0.08f);
        }


        camera = GetNode<Camera3D>("camera");
    }

	private MeshInstance3D CreateMarker(Color color, float radius)
	{
		var marker = new MeshInstance3D();
		marker.Mesh = new SphereMesh { Radius = radius, Height = radius * 2f };
		marker.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true,
		};
		// TopLevel means the marker ignores this body's rotation and lives in world space.
		marker.TopLevel = true;
		AddChild(marker);
		return marker;
	}

	// The only place the contact list can be read.
	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		ContactCount = Mathf.Min(state.GetContactCount(), MaxContacts);
		for (int i = 0; i < ContactCount; i++)
		{
			// Despite the name, these come back in global coordinates.
			ContactPoints[i] = state.GetContactLocalPosition(i);
			ContactNormals[i] = state.GetContactLocalNormal(i);
		}

		// Cap roll speed here rather than in _PhysicsProcess: this is the one
		// place writing angular velocity is authoritative. Airborne spin gets a
		// tighter cap, since no contact friction is there to shed it.
		float spinCap = ContactCount > 0 ? MaxAngularSpeed : MaxAirAngularSpeed;
		if (state.AngularVelocity.Length() > spinCap)
		{
			state.AngularVelocity = state.AngularVelocity.Normalized() * spinCap;
		}
	}

	public void Bounce(){

	    for(int i = 0; i < ContactCount; i++){
            ApplyImpulse(ContactNormals[i] * (7/ ContactCount), ContactPoints[i]);
		}
	}
	// Input is read against the camera, not the world axes: forward means away
	// from the camera, which is what a platformer player expects.
	private Vector3 CameraRelative(Vector2 inputDir)
	{
		Camera3D camera = GetViewport().GetCamera3D();
		if (camera == null)
		{
			return new Vector3(inputDir.X, 0, inputDir.Y);
		}

		Basis basis = camera.GlobalBasis;
		// Flatten to the ground plane so looking down does not shrink the input.
		Vector3 back = new Vector3(basis.Z.X, 0, basis.Z.Z).Normalized();
		Vector3 right = new Vector3(basis.X.X, 0, basis.X.Z).Normalized();
		return right * inputDir.X + back * inputDir.Y;
	}

	// Movement runs on the fixed physics tick, not the render frame.
	public override void _PhysicsProcess(double delta)
	{
		Vector2 inputDir = Input.GetVector("left", "right", "up", "down");
		Vector3 dir = CameraRelative(inputDir);

		// Rolling along +X needs spin about -Z, along +Z needs spin about +X.
		// Friction at the contact patch turns that spin into travel.
		ApplyTorque(new Vector3(dir.Z, 0, -dir.X) * TorqueStrength);

		// Airborne there is nothing to push against, so nudge directly. The nudge
		// only counts while you are slow along the input direction, so it steers a
		// jump without letting you fly.
		if (ContactCount == 0 && dir != Vector3.Zero)
		{
			Vector3 wish = dir.Normalized();
			Vector3 horizontal = new Vector3(LinearVelocity.X, 0, LinearVelocity.Z);
			if (horizontal.Dot(wish) < MaxAirSpeed)
			{
				ApplyCentralForce(dir * AirControl);
			}
		}
		else if (ContactCount > 0 && dir.LengthSquared() < 0.01f)
		{
			// Hands off and grounded: bleed off spin so the roll stops on demand.
			// Proportional, so it eases out instead of slamming to a halt.
			ApplyTorque(-AngularVelocity * BrakeStrength);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_comMarker.GlobalPosition = GlobalPosition + GlobalBasis * CenterOfMass;

        for (int i = 0; i < MaxContacts; i++)
        {
            bool active = i < ContactCount;
            _contactMarkers[i].Visible = active;
            if (active)
            {
                _contactMarkers[i].GlobalPosition = ContactPoints[i];
            }
        }
        if (Input.IsActionJustPressed("space"))
        {
            if (bouncedDebounce < 0)
            {

                Bounce();
                bouncedDebounce = 1;
            }

        }

        if(bouncedDebounce >0){
            bouncedDebounce -= (float)delta;
        }
	}

}
