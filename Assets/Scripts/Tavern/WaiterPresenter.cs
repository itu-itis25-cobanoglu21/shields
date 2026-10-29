using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shields.Players;
using Shields.Social;
using Shields.Table;

namespace Shields.Tavern
{
    /// <summary>
    /// Plays one waiter delivery on this machine from a <see cref="WaiterDeliveryPlan"/>.
    /// Every machine runs the same plan locally, so the waiter is never a NetworkObject.
    ///
    /// The presenter decides nothing about who is served. At each stop it raises
    /// <see cref="StopReached"/> and waits for <see cref="SetHandoffDecision"/>: offline
    /// the local authority answers at once, online the host broadcasts its answer to
    /// everyone. When the waiter is fully outside again it raises <see cref="Exited"/>,
    /// and only the authority turns that into a queue completion.
    ///
    /// Route: spawn outside, beside the doorway where no seat can see it → through the
    /// doorway → the ring outside the chairs → in to each stop (behind the recipient's
    /// LEFT shoulder, in that seat's own frame) → back out → round to the door → out →
    /// removed. The mug goes onto the recipient's LEFT, so the serving hand stays on that
    /// side and never crosses the seated player.
    ///
    /// Every route point lies on the measured floor surface, so the waiter's ground point
    /// (its invisible feet) is on the floor at the lowest point of each step.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaiterPresenter : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] WaiterCharacter waiterPrefab;

        [Tooltip("Project-owned variant of the user's mug, at its table scale.")]
        [SerializeField] GameObject beerMugPrefab;

        [Header("Scene")]
        [SerializeField] TableLayoutController tableLayout;

        [Tooltip("Outside, beside the doorway, hidden behind the wall from every seat.")]
        [SerializeField] Transform waiterSpawn;

        [Tooltip("Just outside the doorway, on its centre line.")]
        [SerializeField] Transform doorOutside;

        [Tooltip("Just inside the doorway, on its centre line.")]
        [SerializeField] Transform doorInside;

        [Tooltip("Outside, on the other side of the doorway, hidden from every seat.")]
        [SerializeField] Transform waiterExit;

        [Header("Ground")]
        [Tooltip("World height of the tavern's walking surface: the top of the floor tiles (Environment/Tavern/floorN mesh bounds top, measured 0.041 m). The waiter's ground point is put here.")]
        [SerializeField] float floorSurfaceHeight = 0.041f;

        [Header("Route")]
        [Tooltip("Ring radius beyond the seat ring. Keeps the waiter and tray clear of chairs and players.")]
        [SerializeField] float ringClearance = 1.0f;

        [Tooltip("How far behind the seat ring the waiter stands to serve.")]
        [SerializeField] float stopClearance = 0.62f;

        [Tooltip("Stops sit this many degrees round the ring from the recipient toward their LEFT, behind their left shoulder (capped at 45% of a seat step).")]
        [SerializeField] float stopAngle = 22f;

        [SerializeField] float walkSpeed = 1.1f;

        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 320f;

        [Tooltip("Distance over which walking eases in and out.")]
        [SerializeField] float easeDistance = 0.35f;

        [Header("Service Timing (seconds)")]
        [SerializeField] float stopSettle = 0.15f;
        [SerializeField] float reachTray = 0.17f;
        [SerializeField] float grabLift = 0.10f;
        [SerializeField] float extendToPlayer = 0.35f;
        [SerializeField] float placePause = 0.12f;
        [SerializeField] float returnHand = 0.25f;

        [Tooltip("Pause at a stop whose recipient may no longer be served.")]
        [SerializeField] float skipPause = 0.35f;

        [Tooltip("A remote machine that never hears the host's decision for a stop skips it after this long.")]
        [SerializeField] float decisionTimeout = 6f;

        [Header("Hand")]
        [Tooltip("The hand hovers this far above a mug's rim while carrying it.")]
        [SerializeField] float gripAboveRim = 0.035f;

        [SerializeField] float liftHeight = 0.07f;

        [Tooltip("Extra height at the middle of the carry, so the mug arcs instead of sliding.")]
        [SerializeField] float carryArc = 0.10f;

        [Tooltip("The carry curves through a point this far out to the recipient's LEFT of their body centre line, so the hand and mug pass beside the shoulder, never through the player.")]
        [SerializeField] float leftClearance = 0.40f;

        [Tooltip("Height above the floor of that clearance point: around the seated shoulder.")]
        [SerializeField] float clearanceHeight = 1.02f;

        /// <summary>The waiter reached a stop: order id, seat. The authority answers with a decision.</summary>
        public event Action<int, int> StopReached;

        /// <summary>The delivery is over on this machine: order id, aborted.</summary>
        public event Action<int, bool> Exited;

        readonly Dictionary<int, bool> _decisions = new Dictionary<int, bool>();
        readonly List<int> _delivered = new List<int>();
        readonly List<int> _skipped = new List<int>();
        readonly List<Vector3> _path = new List<Vector3>();

        WaiterCharacter _waiter;
        Coroutine _run;
        WaiterDeliveryPlan _plan;
        bool _authorityEnded;
        float _ringAngle;
        float _mugHeight = 0.115f;

        public bool IsRunning { get { return _run != null; } }

        public int ActiveOrderId { get { return _run != null ? _plan.OrderId : BeerOrderQueue.NoOrder; } }

        /// <summary>The live waiter, or null. Diagnostics only.</summary>
        public WaiterCharacter Waiter { get { return _waiter; } }

        /// <summary>Seats served / skipped so far in the current or last delivery. Diagnostics only.</summary>
        public IReadOnlyList<int> DeliveredSeats { get { return _delivered; } }
        public IReadOnlyList<int> SkippedSeats { get { return _skipped; } }

        /// <summary>Phase name for diagnostics: Entering, Ring, Serving, Leaving, Idle.</summary>
        public string Stage { get; private set; } = "Idle";

        // ------------------------------------------------------------------ geometry (shared with the planner)

        TableLayoutMetrics Metrics { get { return tableLayout.Metrics; } }

        float RingRadius { get { return Metrics.SeatRadius + ringClearance; } }

        float StopRadius { get { return Metrics.SeatRadius + stopClearance; } }

        float StopOffset { get { return Mathf.Min(stopAngle, Metrics.SeatAngleStep * 0.45f); } }

        float DoorAngle
        {
            get { return doorInside != null ? AngleOf(doorInside.position - tableLayout.TableCenter) : 0f; }
        }

        /// <summary>True when the table and the route anchors exist, so a delivery can be shown.</summary>
        public bool CanPresent
        {
            get
            {
                return waiterPrefab != null && beerMugPrefab != null && tableLayout != null &&
                    tableLayout.Metrics.PlayerCount > 0 && tableLayout.TableAreas.Count > 0 &&
                    waiterSpawn != null && doorOutside != null && doorInside != null && waiterExit != null;
            }
        }

        /// <summary>
        /// Where each recipient's stop lies around the ring, in degrees from the door.
        /// The authority plans the walking order from these; every machine derives the
        /// same numbers from the same deterministic table layout.
        /// </summary>
        public double[] StopAnglesFromDoor(IReadOnlyList<int> seats)
        {
            double[] angles = new double[seats.Count];
            for (int i = 0; i < seats.Count; i++)
                angles[i] = WaiterRoutePlanner.Normalize(StopAbsoluteAngle(seats[i]) - DoorAngle);

            return angles;
        }

        float StopAbsoluteAngle(int seat)
        {
            // Seat yaw matches SeatLayout: a seat at yaw a sits at Euler(0, a, 0) * back.
            // Toward higher yaw is the seated player's LEFT (seat 0 faces +Z, and +yaw moves
            // round toward -X), so this is behind their left shoulder in their own frame.
            return SeatLayout.SeatYaw(seat, Metrics) + StopOffset;
        }

        Vector3 PointOnCircle(float absoluteDegrees, float radius)
        {
            Vector3 center = Flat(tableLayout.TableCenter);
            return center + Quaternion.Euler(0f, absoluteDegrees, 0f) * (Vector3.back * radius);
        }

        static float AngleOf(Vector3 fromCenter)
        {
            return Mathf.Atan2(-fromCenter.x, -fromCenter.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ control

        /// <summary>Starts presenting a delivery. A delivery that cannot be shown ends at once.</summary>
        public void Begin(WaiterDeliveryPlan plan)
        {
            if (_run != null)
            {
                if (_plan.OrderId == plan.OrderId) return;

                Abort();
            }

            _plan = plan;
            _decisions.Clear();
            _delivered.Clear();
            _skipped.Clear();
            _authorityEnded = false;

            if (!plan.IsValid || !CanPresent)
            {
                Debug.LogWarning("Waiter delivery #" + plan.OrderId + " cannot be presented; ending it.", this);
                RaiseExited(plan.OrderId, false);
                return;
            }

            _run = StartCoroutine(Run());
        }

        /// <summary>The authority's answer for one stop.</summary>
        public void SetHandoffDecision(int orderId, int seat, bool deliver)
        {
            if (_run == null || orderId != _plan.OrderId) return;

            _decisions[seat] = deliver;
        }

        /// <summary>
        /// The authority has finished this order. A machine still walking finishes its
        /// walk out; any stop it never heard a decision for is skipped.
        /// </summary>
        public void MarkEnded(int orderId)
        {
            if (_run != null && orderId == _plan.OrderId) _authorityEnded = true;
        }

        /// <summary>Removes the waiter at once, undelivered mugs with it. Match end or teardown.</summary>
        public void Abort()
        {
            if (_run == null && _waiter == null) return;

            int orderId = _plan.OrderId;
            bool wasRunning = _run != null;

            if (_run != null) StopCoroutine(_run);
            _run = null;
            DestroyWaiter();
            Stage = "Idle";

            if (wasRunning) RaiseExited(orderId, true);
        }

        /// <summary>
        /// Removes every delivered mug from every DrinkArea. Used when a match table goes
        /// away or a new match starts on a layout that was kept standing (online), so
        /// beers never leak into a later match. Returns how many were removed.
        /// </summary>
        public int ClearDeliveredMugs()
        {
            if (tableLayout == null) return 0;

            int removed = 0;

            // A mug being drunk is in a hand, not in its slot: it goes too.
            IReadOnlyList<BeerMugVisual> mugs = BeerMugVisual.Active;
            for (int i = mugs.Count - 1; i >= 0; i--)
            {
                BeerMugVisual mug = mugs[i];
                if (mug == null || !mug.IsDelivered || mug.IsHome) continue;

                mug.transform.SetParent(null, true);
                Destroy(mug.gameObject);
                removed++;
            }

            foreach (PlayerTableArea area in tableLayout.TableAreas)
            {
                if (area == null) continue;

                Transform drinkArea = area.DrinkArea;
                for (int i = drinkArea.childCount - 1; i >= 0; i--)
                {
                    // Detached first: Destroy is deferred, and the slot count picks the next slot.
                    GameObject slot = drinkArea.GetChild(i).gameObject;
                    slot.transform.SetParent(null, false);
                    Destroy(slot);
                    removed++;
                }
            }

            return removed;
        }

        void OnDisable()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
            DestroyWaiter();
        }

        void DestroyWaiter()
        {
            if (_waiter != null) Destroy(_waiter.gameObject);
            _waiter = null;
        }

        void RaiseExited(int orderId, bool aborted)
        {
            Action<int, bool> exited = Exited;
            if (exited != null) exited(orderId, aborted);
        }

        // ------------------------------------------------------------------ the delivery

        IEnumerator Run()
        {
            WaiterDeliveryPlan plan = _plan;

            Stage = "Entering";
            Vector3 spawn = Flat(waiterSpawn.position);
            Quaternion facing = Quaternion.LookRotation(Flat(doorOutside.position) - spawn, Vector3.up);
            _waiter = Instantiate(waiterPrefab, spawn, facing);
            _waiter.name = "Waiter_Order_" + plan.OrderId;
            WaiterCharacter.StripPhysics(_waiter.gameObject);

            // One real mug per recipient: mug i belongs to VisitSeats[i].
            _waiter.LoadMugs(beerMugPrefab, plan.VisitSeats.Length);
            CacheMugHeight();

            float door = DoorAngle;
            float start = plan.Mode == WaiterRouteMode.BackwardAndBack ? 360f : 0f;
            _ringAngle = start;

            _path.Clear();
            _path.Add(Flat(doorOutside.position));
            _path.Add(Flat(doorInside.position));
            _path.Add(PointOnCircle(door + start, RingRadius));
            yield return Walk(_path);

            double[] stopAngles = StopAnglesFromDoor(plan.VisitSeats);

            for (int i = 0; i < plan.VisitSeats.Length; i++)
            {
                int seat = plan.VisitSeats[i];
                float target = (float)stopAngles[i];

                // Backward routes count down from 360 so the walk never wraps through the door.
                if (plan.Mode == WaiterRouteMode.BackwardAndBack && target <= 0f) target = 360f;

                Stage = "Ring";
                yield return WalkRing(door, target);

                Stage = "Serving";
                Vector3 ringPoint = PointOnCircle(door + target, RingRadius);
                Vector3 stopPoint = PointOnCircle(door + target, StopRadius);

                _path.Clear();
                _path.Add(stopPoint);
                yield return Walk(_path);

                yield return ServeAt(plan.OrderId, seat, i);

                _path.Clear();
                _path.Add(ringPoint);
                yield return Walk(_path);
            }

            Stage = "Leaving";
            yield return WalkRing(door, start == 360f || plan.Mode == WaiterRouteMode.FullLoop ? 360f : 0f);

            _path.Clear();
            _path.Add(Flat(doorInside.position));
            _path.Add(Flat(doorOutside.position));
            _path.Add(Flat(waiterExit.position));
            yield return Walk(_path);

            // Fully outside: only now is the delivery over.
            _run = null;
            DestroyWaiter();
            Stage = "Idle";
            RaiseExited(plan.OrderId, false);
        }

        IEnumerator ServeAt(int orderId, int seat, int mugIndex)
        {
            PlayerTableArea area = tableLayout.GetAreaForPlayer(seat);
            if (area == null)
            {
                _skipped.Add(seat);
                yield break;
            }

            // Face the slot this mug will take: the next free one.
            Transform drinkArea = area.DrinkArea;
            Vector3 nextSlotLocal;
            Quaternion nextSlotRotation;
            DrinkAreaLayout.Pose(drinkArea.childCount, Metrics, out nextSlotLocal, out nextSlotRotation);
            yield return FaceTowards(drinkArea.TransformPoint(nextSlotLocal));
            yield return new WaitForSeconds(stopSettle);

            Action<int, int> reached = StopReached;
            if (reached != null) reached(orderId, seat);

            // Revalidated by the authority right before the handoff.
            float waited = 0f;
            bool deliver;
            while (!_decisions.TryGetValue(seat, out deliver))
            {
                if (_authorityEnded || waited > decisionTimeout)
                {
                    deliver = false;
                    break;
                }

                waited += Time.deltaTime;
                yield return null;
            }

            if (!deliver)
            {
                // The recipient is gone: their mug stays on the tray and leaves with the waiter.
                _skipped.Add(seat);
                yield return new WaitForSeconds(skipPause);
                yield break;
            }

            Transform mug = _waiter.Mugs.Count > mugIndex ? _waiter.Mugs[mugIndex] : null;

            // The table can be torn down while the waiter stands here (match end); the
            // coordinator aborts on its next update, so just do nothing more.
            if (mug == null || drinkArea == null)
            {
                _skipped.Add(seat);
                yield break;
            }

            yield return HandOver(mug, mugIndex, drinkArea, seat);
            if (mug != null && mug.parent != null && mug.parent.parent == drinkArea) _delivered.Add(seat);
        }

        /// <summary>
        /// Tray → delivery hand → DrinkArea, with the very same mug object throughout.
        /// </summary>
        IEnumerator HandOver(Transform mug, int mugIndex, Transform drinkArea, int seat)
        {
            WaiterCharacter waiter = _waiter;
            Transform hand = waiter.DeliveryHand;
            Transform rest = waiter.DeliveryHandRest;
            waiter.DeliveryHandBusy = true;

            // The next free drink slot, reserved now so a later order can never take it.
            int slotIndex = drinkArea.childCount;
            Vector3 slotLocal;
            Quaternion slotRotationLocal;
            DrinkAreaLayout.Pose(slotIndex, Metrics, out slotLocal, out slotRotationLocal);

            Transform slot = new GameObject("DrinkSlot_" + slotIndex).transform;
            slot.SetParent(drinkArea, false);
            slot.localPosition = slotLocal;
            slot.localRotation = slotRotationLocal;

            float above = _mugHeight + gripAboveRim;

            // 1. Reach to the mug on the tray.
            Vector3 from = hand.position;
            yield return Animate(reachTray, t =>
            {
                if (mug == null) return;
                hand.position = Vector3.Lerp(from, mug.position + Vector3.up * above, t);
                hand.rotation = rest.rotation;
            });

            if (TableLost(slot, mug)) yield break;

            // 2. Take it: the tray loses this mug, the hand carries it.
            waiter.ReleaseMug(mugIndex);
            Transform traySlot = mug.parent;
            mug.SetParent(hand, true);
            if (traySlot != null && traySlot != hand) Destroy(traySlot.gameObject);

            from = hand.position;
            yield return Animate(grabLift, t =>
            {
                hand.position = from + Vector3.up * (liftHeight * t);
                hand.rotation = rest.rotation;
            });

            if (TableLost(slot, mug)) yield break;

            // 3. Carry it over to the player's DrinkArea along a small curve that passes
            //    beside the recipient's LEFT shoulder, never through their body.
            from = hand.position;
            Vector3 mugToHand = hand.position - mug.position;
            Vector3 handTarget = slot.position + mugToHand;
            Vector3 through = CarryClearancePoint(seat, from, handTarget, mugToHand);
            Quaternion mugFrom = mug.rotation;
            yield return Animate(extendToPlayer, t =>
            {
                if (slot == null || mug == null) return;
                hand.position = CurveThrough(from, through, handTarget, t);
                hand.rotation = rest.rotation;
                mug.rotation = Quaternion.Slerp(mugFrom, slot.rotation, t);
            });

            if (TableLost(slot, mug)) yield break;

            // 4. Set it down: the mug now belongs to the table, not the waiter.
            Vector3 scale = beerMugPrefab.transform.localScale;
            mug.SetParent(slot, false);
            mug.localPosition = Vector3.zero;
            mug.localRotation = Quaternion.identity;
            mug.localScale = scale;
            mug.name = "BeerMug_Order" + _plan.OrderId + "_Seat" + seat;

            // From now on this mug is a drinkable beer, full, at home in this slot.
            BeerMugVisual visual = mug.GetComponent<BeerMugVisual>();
            if (visual != null) visual.MarkDelivered(new BeerId(_plan.OrderId, seat), slot);

            yield return new WaitForSeconds(placePause);

            // 5. Hand back to rest.
            from = hand.position;
            yield return Animate(returnHand, t =>
            {
                hand.position = Vector3.Lerp(from, rest.position, t);
                hand.rotation = rest.rotation;
            });

            waiter.DeliveryHandBusy = false;
        }

        /// <summary>
        /// The point the carrying hand passes through halfway: the straight line's midpoint,
        /// pushed out to the recipient's LEFT until it clears their shoulder by
        /// <see cref="leftClearance"/>, and lifted to shoulder height. Built in the
        /// recipient's own frame, so it works for every seat. Only ever moves the path
        /// further left, never toward the player.
        /// </summary>
        Vector3 CarryClearancePoint(int seat, Vector3 from, Vector3 to, Vector3 mugToHand)
        {
            Vector3 mid = (from + to) * 0.5f + Vector3.up * carryArc;

            PlayerCharacter player = tableLayout.GetPlayer(seat);
            if (player == null) return mid;

            Transform body = player.transform;
            Vector3 left = -body.right;
            left.y = 0f;
            left.Normalize();

            // The mug's centre must clear the shoulder; the hand rides above it.
            float midLateral = Vector3.Dot(mid - mugToHand - body.position, left);
            float push = Mathf.Max(0f, leftClearance - midLateral);

            Vector3 point = mid + left * push;

            float minHandY = floorSurfaceHeight + clearanceHeight + mugToHand.y;
            point.y = Mathf.Max(point.y, minHandY);
            return point;
        }

        /// <summary>Quadratic curve from a to c that passes exactly through b at t = 0.5.</summary>
        static Vector3 CurveThrough(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            Vector3 control = 2f * b - 0.5f * (a + c);
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * c;
        }

        /// <summary>
        /// True when the target table area went away mid-handoff (match teardown). The
        /// handoff stops quietly and the coordinator's abort removes the waiter.
        /// </summary>
        bool TableLost(Transform slot, Transform mug)
        {
            if (slot != null && mug != null) return false;

            if (_waiter != null) _waiter.DeliveryHandBusy = false;
            return true;
        }

        // ------------------------------------------------------------------ movement

        IEnumerator WalkRing(float door, float targetFromDoor)
        {
            float from = _ringAngle;
            if (Mathf.Abs(targetFromDoor - from) < 0.01f) yield break;

            _path.Clear();
            float direction = Mathf.Sign(targetFromDoor - from);
            const float stepDegrees = 6f;

            for (float a = from + direction * stepDegrees; direction > 0f ? a < targetFromDoor : a > targetFromDoor;
                 a += direction * stepDegrees)
                _path.Add(PointOnCircle(door + a, RingRadius));

            _path.Add(PointOnCircle(door + targetFromDoor, RingRadius));
            _ringAngle = targetFromDoor;

            yield return Walk(_path);
        }

        /// <summary>Walks a polyline at an eased constant speed, facing where it is going.</summary>
        IEnumerator Walk(List<Vector3> points)
        {
            if (_waiter == null || points.Count == 0) yield break;

            List<Vector3> path = new List<Vector3>(points.Count + 1) { Flat(_waiter.transform.position) };
            path.AddRange(points);

            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            if (total < 0.001f) yield break;

            float travelled = 0f;
            while (travelled < total)
            {
                float ease = Mathf.Min((travelled + 0.05f) / easeDistance, (total - travelled) / easeDistance);
                float speed = walkSpeed * Mathf.Clamp(ease, 0.25f, 1f);

                travelled = Mathf.Min(total, travelled + speed * Time.deltaTime);
                Vector3 position = PointAlong(path, travelled);
                Vector3 ahead = PointAlong(path, Mathf.Min(total, travelled + 0.25f)) - position;

                _waiter.transform.position = position;
                if (ahead.sqrMagnitude > 0.0001f)
                    _waiter.transform.rotation = Quaternion.RotateTowards(_waiter.transform.rotation,
                        Quaternion.LookRotation(ahead, Vector3.up), turnSpeed * Time.deltaTime);

                _waiter.WalkWeight = speed / walkSpeed;
                yield return null;
            }

            _waiter.WalkWeight = 0f;
        }

        IEnumerator FaceTowards(Vector3 point)
        {
            Vector3 direction = Flat(point) - Flat(_waiter.transform.position);
            if (direction.sqrMagnitude < 0.0001f) yield break;

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            while (Quaternion.Angle(_waiter.transform.rotation, target) > 0.5f)
            {
                _waiter.transform.rotation = Quaternion.RotateTowards(_waiter.transform.rotation, target,
                    turnSpeed * Time.deltaTime);
                yield return null;
            }
        }

        static IEnumerator Animate(float duration, Action<float> step)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                step(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration)));
                yield return null;
            }

            step(1f);
        }

        static Vector3 PointAlong(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= segment) return Vector3.Lerp(path[i - 1], path[i], segment > 0f ? distance / segment : 1f);

                distance -= segment;
            }

            return path[path.Count - 1];
        }

        Vector3 Flat(Vector3 point)
        {
            return new Vector3(point.x, floorSurfaceHeight, point.z);
        }

        void CacheMugHeight()
        {
            for (int i = 0; i < _waiter.Mugs.Count; i++)
            {
                Renderer renderer = _waiter.Mugs[i] != null ? _waiter.Mugs[i].GetComponentInChildren<Renderer>() : null;
                if (renderer == null) continue;

                _mugHeight = renderer.bounds.size.y;
                return;
            }
        }
    }
}
