using System;
using UnityEngine;

namespace WalkaboutDesktop;

/// Replaces the game's rolling braking while the ball is on a custom lane.
/// The game brakes via Rigidbody.angularDrag (2 while rolling, up to 10 near rest), i.e. purely speed-proportional.
/// Real minigolf balls brake with a roughly constant rolling resistance plus a smaller speed-dependent part:
///     a(v) = rollDecel + rollDrag * v      (m/s^2)
/// We switch the game's angular drag off and scale linear and angular velocity together each physics step,
/// which keeps the ball rolling (no skid) while applying exactly a(v).
public static class BallPhysics
{
    public static bool Enabled = true;
    public static float RollDecel = 0.40f;   // m/s^2, constant part (rolling resistance) - estimate, not measured yet
    public static float RollDrag = 0.10f;    // 1/s, speed-proportional part - estimate
    public static float StopSpeed = 0.01f;   // m/s, below this the ball is stopped
    public static bool OnLane { get; private set; }
    public static float LastSpeed { get; private set; }

    public static void FixedStep()
    {
        OnLane = false;
        if (!Enabled || !LaneBuilder.Active) return;
        var ball = Ball.instance;
        if (ball == null) return;
        var rb = ball.rb;
        if (rb == null || rb.isKinematic) return;

        // only while the ball is on (or just above) the playing surface of our lane, not in the cup or in the air
        var l = LaneBuilder.LaneAt(rb.position);
        if (l == null) return;
        var d = l.Def;
        var local = l.ToLocal(rb.position);
        float r = ball.radius > 0 ? ball.radius : 0.021f;
        if (!LaneGeometry.Inside(d, local.x, local.z)) return;
        // Anywhere over our lane (also airborne, in loops, tubes, bowls) the game's speed-proportional damping is off;
        // our rolling resistance below applies only while the ball rolls on the playing surface.
        rb.angularDrag = 0f;
        rb.drag = 0f;
        float hs = LaneGeometry.Height(d, local.x, local.z);
        var g = LaneGeometry.Gradient(d, local.x, local.z);
        float slope = Mathf.Sqrt(g.x * g.x + g.y * g.y);
        float cos = 1f / Mathf.Sqrt(1f + slope * slope);
        float above = (local.y - hs) * cos; // distance of the ball centre from the surface along its normal
        if (above < r - 0.01f || above > r + 0.015f) return; // in the cup / airborne / on top of an obstacle

        OnLane = true;
        rb.angularDrag = 0f;
        rb.drag = 0f;

        var v = rb.velocity;
        float speed = v.magnitude;
        LastSpeed = speed;
        float dt = Time.fixedDeltaTime;
        // Rolling resistance can hold the ball on a slope only if gravity along the slope (rolling sphere: 5/7 g sin) is weaker.
        float sin = slope * cos;
        bool canRest = 5f / 7f * 9.81f * sin <= RollDecel;
        if (speed < 1e-4f) return;

        float decel = RollDecel + RollDrag * speed;
        float newSpeed = speed - decel * dt;
        if (newSpeed <= StopSpeed && canRest)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }
        float f = Mathf.Max(0f, newSpeed) / speed;
        rb.velocity = v * f;
        rb.angularVelocity *= f;
    }
}
