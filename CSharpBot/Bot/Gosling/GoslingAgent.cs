﻿using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using Bot.Gosling.Objects;
using Bot.Gosling.Routines;
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
        private List<Player> friends = new();
        private List<Player> foes = new();
        private Player me;

        // Porting note: the original GoslingUtils code makes a new class for Ball and GameInfo for easier abstraction
        // (similar to BoostObject and GoalObject). We don't need this in the C# code because it comes with nice
        // structures for Ball and GameInfo by default with the example bot template.
        // However, there are no equivalents to BoostObject and GoalObject by default, so we need to create those
        // abstractions.

        private Ball ball;
        private GameInfo gameInfo;
        private List<BoostObject> boosts = new();
        private GoalObject friendGoal;
        private GoalObject foeGoal;

        private Stack<IRoutine> stack;
        private float time;
        private bool ready = false;

        private Controller controller = new();
        private bool kickoffFlag = false;

        public GoslingAgent(string name, int team, int index) : base(name, team, index)
        {
            // Porting note: we don't set ball and gameInfo here unlike the original because we don't have the same
            // type of abstraction as friendGoal and foeGoal. These fields (ball and gameInfo) will get set in the
            // Preprocess() method instead, when we have actual information about the ball and game.

            friendGoal = new GoalObject(team);
            foeGoal = new GoalObject(1 - team);
        }

        private void GetReady(Packet packet)
        {
            var fieldInfo = GetFieldInfo();
            for (int i = 0; i < fieldInfo.BoostPads.Length; i++)
            {
                var boost = fieldInfo.BoostPads[i];
                boosts.Add(new BoostObject(i, boost.Location, boost.IsFullBoost));
            }
            
            ball = packet.Ball;
            ready = true;
        }

        // Porting note: at this point of the code, the original has a method called refresh_player_lists. We don't need
        // this because we are refreshing the player lists anyway in every Preprocess call.

        // Porting note: at this point of the code, the original has 2 convenience methods to push and pop the stack.
        // We don't need that in the C# version because we use an actual Stack data structure instead of using a List
        // like the original, so we don't need any convenience methods.
        // Permalink to original: https://github.com/ddthj/GoslingUtils/blob/733b6b05bc9cab8da596d6ed324fbfbf179100a0/objects.py#L54-L60

        void Line(Vector3 start, Vector3 end, Color? color = null)
        {
            color ??= Colors.White;
            Renderer.DrawLine3D(color.Value, start, end);
        }

        /// <summary>
        /// Draws the stack on the screen
        /// </summary>
        void DebugStack()
        {
            var enumerator = stack.GetEnumerator();
            for (int i = 0; i < stack.Count; i++)
            {
                var text = enumerator.Current.GetType().Name;
                var upperLeft = new Vector2(10, 50 + 50 * (stack.Count - i));
                Renderer.DrawString2D(text, Colors.White, upperLeft, 3, 3);

                enumerator.MoveNext();
            }
            enumerator.Dispose();
        }

        // Porting note: here the original code has a convenience method, clear(), for the stack that we don't need.
        // See the comments above for explanations.

        void Preprocess(Packet packet)
        {
            // Calling the update functions for all of the objects
            
            // Porting note: we don't update the car objects because we use Player. Therefore, we just get new instances
            // of the friends and foes instead.
            friends.Clear();
            foes.Clear();
            for (int i = 0; i < packet.Players.Length; i++)
            {
                if (i == index)
                    me = packet.Players[i];
                else if (packet.Players[i].Team == team)
                    friends.Add(packet.Players[i]);
                else
                    foes.Add(packet.Players[i]);
            }

            foreach (var pad in boosts)
                pad.Update(packet);

            ball = packet.Ball;
            gameInfo = packet.GameInfo;
            time = packet.GameInfo.SecondsElapsed;

            // When a new kickoff begins we empty the stack
            var isKickoff = packet.GameInfo.IsRoundActive && packet.GameInfo.IsKickoffPause;
            if (!kickoffFlag && isKickoff)
                stack.Clear();

            // Tells us when to go for kickoff
            kickoffFlag = isKickoff;
        }

        public override Controller GetOutput(rlbot.flat.GameTickPacket gameTickPacket)
        {
            Packet packet = new Packet(gameTickPacket);

            // Reset controller
            controller = new();

            // Get ready, then preprocess
            if (!ready)
                GetReady(packet);
            Preprocess(packet);

            // Porting note: we don't need to begin and end rendering for the Renderer unlike in Python

            // Run our strategy code
            Run();

            // Run the routine on the end of the stack
            if (stack.Count > 0)
                stack.Peek().Run(this);

            // Send our updated controller back to RLBot
            return controller;
        }

        protected virtual void Run()
        {
            // Override this with your strategy code
        }

        // Porting note: this is not in the original code, because there is no need to do it in Python. For C# we really
        // need to convert the rlbot.flat.FieldInfo object into the nicer to use structure provided in the example bot.
        // In fact, this code is taken from the example bot template.

        // Hide the old methods that return Flatbuffers objects and use our own methods that
        // use processed versions of those objects instead.
        internal new FieldInfo GetFieldInfo() => new FieldInfo(base.GetFieldInfo());
    }
}