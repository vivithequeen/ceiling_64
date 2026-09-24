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
	// A contact only counts as "ground" if its normal points mostly upward; this
	// keeps wall and ceiling scrapes from spawning floor dust.
	[Export] public float GroundNormalThreshold = 0.5f; //AI CODE
	[Export] public float ParticleGroundOffset = 0.02f; //AI CODE
	// Below this closing speed a touchdown is a scuff, not a landing, so it gets
	// no burst.
	[Export] public float FallParticleMinSpeed = 3f; //AI CODE

	// The instanced floor_partical scene root (top_level, so it lives in world
	// space) and the emitter inside it.
	private Node3D floorParticleRig; //AI CODE
	private GpuParticles3D floorParticles; //AI CODE

	// The one-shot impact burst, same top_level arrangement as the dust ring.
	private Node3D fallParticleRig; //AI CODE
	private GpuParticles3D fallParticles; //AI CODE
	// Landing is a transition, so it needs last frame's grounded state, plus the
	// velocity from before the collision was resolved to judge how hard we hit.
	private bool wasGrounded; //AI CODE
	private Vector3 previousVelocity; //AI CODE


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




        camera = GetNode<Camera3D>("camera");

        floorParticleRig = GetNodeOrNull<Node3D>("floor_partical"); //AI CODE
        floorParticles = FindEmitter(floorParticleRig); //AI CODE
        if (floorParticles != null) //AI CODE
        { //AI CODE
            floorParticles.Emitting = false; //AI CODE
        } //AI CODE

        fallParticleRig = GetNodeOrNull<Node3D>("fall_particle"); //AI CODE
        fallParticles = FindEmitter(fallParticleRig); //AI CODE
        if (fallParticles != null) //AI CODE
        { //AI CODE
            fallParticles.Emitting = false; //AI CODE
        } //AI CODE
    }

	// Grab the emitter out of an instanced particle scene by type rather than by
	// name: the rigs get rebuilt in the editor and the child's name drifts, which
	// a path lookup would only report as nothing ever emitting. //AI CODE
	private static GpuParticles3D FindEmitter(Node rig) //AI CODE
	{ //AI CODE
		if (rig == null) //AI CODE
		{ //AI CODE
			return null; //AI CODE
		} //AI CODE

		foreach (Node child in rig.GetChildren()) //AI CODE
		{ //AI CODE
			if (child is GpuParticles3D emitter) //AI CODE
			{ //AI CODE
				return emitter; //AI CODE
			} //AI CODE
		} //AI CODE

		return null; //AI CODE
	} //AI CODE

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

		UpdateFloorParticles(); //AI CODE

		// Kept for the next tick: once a collision is resolved the impact speed is
		// already gone from LinearVelocity.
		previousVelocity = LinearVelocity; //AI CODE
	}

	// Park the dust ring on the patch of ground the ball is actually touching,
	// flat against it, and only let it emit while something down there is hit.
	private void UpdateFloorParticles() //AI CODE
	{ //AI CODE
		if (floorParticles == null || floorParticleRig == null) //AI CODE
		{ //AI CODE
			return; //AI CODE
		} //AI CODE

		Vector3 point = Vector3.Zero; //AI CODE
		Vector3 normal = Vector3.Zero; //AI CODE
		int groundContacts = 0; //AI CODE
		for (int i = 0; i < ContactCount; i++) //AI CODE
		{ //AI CODE
			if (ContactNormals[i].Y < GroundNormalThreshold) //AI CODE
			{ //AI CODE
				continue; //AI CODE
			} //AI CODE

			point += ContactPoints[i]; //AI CODE
			normal += ContactNormals[i]; //AI CODE
			groundContacts++; //AI CODE
		} //AI CODE

		floorParticles.Emitting = groundContacts > 0 && (LinearVelocity * new Vector3(1,0,1)).Length() > 0.01; //AI CODE
		if (groundContacts == 0) //AI CODE
		{ //AI CODE
			wasGrounded = false; //AI CODE
			return; //AI CODE
		} //AI CODE

		point /= groundContacts; //AI CODE
		normal = normal.Normalized(); //AI CODE

		// Build an upright-ish basis around the surface normal. Vector3.Up is the
		// natural reference, except when the normal is already (anti)parallel to it.
		Vector3 reference = Mathf.Abs(normal.Y) > 0.99f ? Vector3.Forward : Vector3.Up; //AI CODE
		Vector3 x = reference.Cross(normal).Normalized(); //AI CODE
		Vector3 z = x.Cross(normal).Normalized(); //AI CODE

		// Lift it a hair off the surface so the ring does not z-fight the floor.
		floorParticleRig.GlobalTransform = new Transform3D( //AI CODE
			new Basis(x, normal, z), //AI CODE
			point + normal * ParticleGroundOffset); //AI CODE

		// Only on the frame contact is made; while rolling along the floor we are
		// already grounded and nothing fires.
		if (!wasGrounded) //AI CODE
		{ //AI CODE
			EmitFallParticles(point, normal, x, z); //AI CODE
		} //AI CODE
		wasGrounded = true; //AI CODE
	} //AI CODE

	// Throw the impact burst up off the spot that was hit, aligned to that
	// surface so the debris flies away from it rather than always straight up.
	private void EmitFallParticles(Vector3 point, Vector3 normal, Vector3 x, Vector3 z) //AI CODE
	{ //AI CODE
		if (fallParticles == null || fallParticleRig == null) //AI CODE
		{ //AI CODE
			return; //AI CODE
		} //AI CODE

		// Speed straight into the surface, measured before the bounce ate it.
		float impactSpeed = -previousVelocity.Dot(normal); //AI CODE
		if (impactSpeed < FallParticleMinSpeed) //AI CODE
		{ //AI CODE
			return; //AI CODE
		} //AI CODE

		fallParticleRig.GlobalTransform = new Transform3D( //AI CODE
			new Basis(x, normal, z), //AI CODE
			point + normal * ParticleGroundOffset); //AI CODE
		// Restart rather than Emitting = true: the emitter is one_shot, so this
		// re-fires it even if the previous burst is still in the air. //AI CODE
		fallParticles.Restart(); //AI CODE
	} //AI CODE

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

        for (int i = 0; i < MaxContacts; i++)
        {

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
