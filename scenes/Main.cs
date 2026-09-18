using Godot;
using System;

public partial class Main : Node3D
{
	// Called when the node enters the scene tree for the first time.
    public void _on_area_3d_body_entered(Node3D body){
        if(body.Name == "ceiling"){
            body.GlobalPosition = new Vector3(0, 3, 0);
        }
    }
}
