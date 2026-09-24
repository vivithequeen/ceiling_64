using Godot;
using System;

public partial class Checkpoint : Area3D
{
    public bool isActive = false;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public void _on_body_entered(Node3D body){
        if (body.Name == "ceiling") {
            isActive = true;
        }
    }
}
