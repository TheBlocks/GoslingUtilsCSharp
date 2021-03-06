using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using Bot.Gosling.Objects;
using Bot.Gosling.Routines;
using Bot.Utilities.Processed.BallPrediction;
using Bot.Utilities.Processed.FieldInfo;
using Bot.Utilities.Processed.Packet;
using RLBotDotNet;

namespace Bot.Gosling
{
    /// <summary>
    /// This is the main class of Gosling Utils. It holds/updates information about the game and runs routines.
    /// All utils rely on information being structured and accessed the same way as configured in this class.
    /// </summary>
    public class GoslingAgent : RLBotDotNet.Bot
    {
        public List<CarObject> Friends = new();
        public List<CarObject> Foes = new();
        public CarObject Me;

        // Porting note: the original GoslingUtils code makes a new class for Ball and GameInfo for easier abstraction
        // (similar to CarObject, BoostObject, GoalObject). We don't need this in the C# code because it comes with nice
        // structures for Ball and GameInfo by default with the example bot template.
        // However, there are no equivalents to BoostObject and GoalObject by default, so we need to create those
        // abstractions. CarObject's equivalent is Player but it doesn't have a Local method so we choose to create that
        // abstraction.

        public Ball Ball;
        public GameInfo GameInfo;
        public readonly List<BoostObject> Boosts = new();
        public GoalObject FriendGoal;
        public GoalObject FoeGoal;

        public Stack<IRoutine> Stack = new();
        public float Time; // Game time
        public bool Ready = false; // Whether or not GoslingAgent has run its get_ready() function

        public Controller Controller = new(); // The controller that is returned to the framework after every tick
        public bool KickoffFlag = false; // A flag that tells us when kickoff is happening

        public GoslingAgent(string name, int team, int index) : base(name, team, index)
        {
            // Porting note: we don't set ball and gameInfo here unlike the original because we don't have the same
            // type of abstraction as friendGoal and foeGoal. These fields (ball and gameInfo) will get set in the
            // Preprocess() method instead, when we have actual information about the ball and game.

            Me = new CarObject(index);
            FriendGoal = new GoalObject(team);
            FoeGoal = new GoalObject(1 - team);
        }

        /// <summary>
        /// Preps all of the objects that will be updated during play.
        /// </summary>
        private void GetReady(Packet packet)
        {
            var fieldInfo = GetFieldInfo();
            for (int i = 0; i < fieldInfo.BoostPads.Length; i++)
            {
                var boost = fieldInfo.BoostPads[i];
                Boosts.Add(new BoostObject(i, boost.Location, boost.IsFullBoost));
            }

            Ball = packet.Ball;
            Ready = true;
        }

        /// <summary>
        /// Makes new friend/foe lists.<br/>
        /// Useful to keep separate from GetReady because humans can join/leave a match.
        /// </summary>
        private void RefreshPlayerLists(Packet packet)
        {
            Friends.Clear();
            Foes.Clear();
            for (int i = 0; i < packet.Players.Length; i++)
            {
                if (packet.Players[i].Team == team && i != index)
                    Friends.Add(new CarObject(i, packet));
                if (packet.Players[i].Team != team)
                    Foes.Add(new CarObject(i, packet));
            }
        }

        /// <summary>
        /// Shorthand for adding a routine to the stack
        /// </summary>
        public void Push(IRoutine routine) => Stack.Push(routine);

        /// <summary>
        /// Shorthand for removing a routine from the stack, returns the routine
        /// </summary>
        public IRoutine Pop() => Stack.Pop();

        public void Line(Vector3 start, Vector3 end, Color? color = null)
        {
            color ??= Colors.White;
            Renderer.DrawLine3D(color.Value, start, end);
        }

        /// <summary>
        /// Draws the stack on the screen
        /// </summary>
        public void DebugStack()
        {
            var enumerator = Stack.GetEnumerator();
            for (int i = 0; i < Stack.Count; i++)
            {
                var text = enumerator.Current.GetType().Name;
                var upperLeft = new Vector2(10, 50 + 50 * (Stack.Count - i));
                Renderer.DrawString2D(text, Colors.White, upperLeft, 3, 3);

                enumerator.MoveNext();
            }

            enumerator.Dispose();
        }

        /// <summary>
        /// Shorthand for clearing the stack of all routines
        /// </summary>
        public void Clear() => Stack.Clear();

        private void Preprocess(Packet packet)
        {
            // Calling the update functions for all of the objects
            if (packet.Players.Length != Friends.Count + Foes.Count + 1)
                RefreshPlayerLists(packet);

            foreach (var car in Friends)
                car.Update(packet);
            foreach (var car in Foes)
                car.Update(packet);
            foreach (var pad in Boosts)
                pad.Update(packet);

            Ball = packet.Ball;
            Me.Update(packet);
            GameInfo = packet.GameInfo;
            Time = packet.GameInfo.SecondsElapsed;

            // When a new kickoff begins we empty the stack
            var isKickoff = packet.GameInfo.IsRoundActive && packet.GameInfo.IsKickoffPause;
            if (!KickoffFlag && isKickoff)
                Stack.Clear();

            // Tells us when to go for kickoff
            KickoffFlag = isKickoff;
        }

        public override Controller GetOutput(rlbot.flat.GameTickPacket gameTickPacket)
        {
            Packet packet = new Packet(gameTickPacket);

            // Reset controller
            Controller = new();

            // Get ready, then preprocess
            if (!Ready)
                GetReady(packet);
            Preprocess(packet);

            // Porting note: we don't need to begin and end rendering for the Renderer unlike in Python

            // Run our strategy code
            Run();

            // Run the routine on the end of the stack
            if (Stack.Count > 0)
                Stack.Peek().Run(this);

            // Send our updated controller back to RLBot
            return Controller;
        }

        protected virtual void Run()
        {
            // Override this with your strategy code
        }

        // Porting note: the following GetFieldInfo and GetBallPrediction hiding is not in the original code, because
        // there is no need to do it in Python. For C#, we really need to convert the rlbot.flat.FieldInfo and
        // rlbot.flat.BallPrediction objects into the nicer-to-use structures provided in the example bot.
        // In fact, this code is taken from the example bot template.

        // Hide the old methods that return Flatbuffers objects and use our own methods that
        // use processed versions of those objects instead.
        internal new FieldInfo GetFieldInfo() => new FieldInfo(base.GetFieldInfo());
        internal new BallPrediction GetBallPrediction() => new BallPrediction(base.GetBallPrediction());
    }
}