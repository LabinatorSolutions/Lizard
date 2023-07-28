﻿using System.Diagnostics;

namespace LTChess.Search
{
    public class TimeManager
    {

        /// <summary>
        /// Add this amount of milliseconds to the total search time when checking if the
        /// search should stop, in case the move overhead is very low and the UCI expects
        /// the search to stop very quickly after our time expires.
        /// </summary>
        public const int TimerBuffer = 50;

        /// <summary>
        /// If we got a "movetime" command, we use a smaller buffer to bring the time we actually search
        /// much closer to the requested time.
        /// </summary>
        private const int MoveTimeBuffer = 5;

        /// <summary>
        /// The minimum amount of time to search, regardless of the other limitations of the search.
        /// This only applies to the amount of time that we were told to search for (i.e. "movetime 100").
        /// If we receive a "stop" command from the UCI, this does no apply and we stop as soon as possible.
        /// </summary>
        public const int MinSearchTime = 200;

        /// <summary>
        /// Set to true if the go command has the "movetime" parameter.
        /// </summary>
        public bool HasMoveTime = false;

        /// <summary>
        /// The time in milliseconds that we were explicitly told to search for.
        /// </summary>
        public int MoveTime = 0;

        /// <summary>
        /// The time in milliseconds that the search should stop at.
        /// </summary>
        public int MaxSearchTime = DefaultSearchTime;

        /// <summary>
        /// Set to the value of winc/binv if one was provided during a UCI "go" command.
        /// Only used
        /// </summary>
        public int[] PlayerIncrement;

        /// <summary>
        /// Set to the value of wtime/btime if one was provided during a UCI "go" command.
        /// If the search time gets too close to this, it will stop prematurely so we don't lose on time.
        /// </summary>
        public int[] PlayerTime;

        private static Stopwatch TotalSearchTime = new Stopwatch();


        public TimeManager()
        {
            PlayerIncrement = new int[2] { 0, 0 };
            PlayerTime = new int[2] { SearchConstants.MaxSearchTime, SearchConstants.MaxSearchTime };
        }

        [MethodImpl(Inline)]
        public void StartTimer() => TotalSearchTime.Start();

        [MethodImpl(Inline)]
        public void StopTimer() => TotalSearchTime.Stop();

        [MethodImpl(Inline)]
        public void ResetTimer() => TotalSearchTime.Reset();

        [MethodImpl(Inline)]
        public void RestartTimer() => TotalSearchTime.Restart();

        [MethodImpl(Inline)]
        public double GetSearchTime() => TotalSearchTime.Elapsed.TotalMilliseconds;

        [MethodImpl(Inline)]
        public bool CheckUp(int ToMove)
        {
            bool shouldStop = false;
            int playerTimeLeft = PlayerTime[ToMove];

            double currentTime = TotalSearchTime.Elapsed.TotalMilliseconds;

            if (currentTime > (MaxSearchTime - (HasMoveTime ? MoveTimeBuffer : TimerBuffer)))
            {
                //  Stop if we are close to going over the max time
                Log("Stopping normally! Used " + currentTime + " of allowed " + MaxSearchTime + "ms" + GetFormattedTime());

                shouldStop = true;
            }
            else if (MaxSearchTime >= playerTimeLeft && (playerTimeLeft - currentTime) < SearchLowTimeThreshold)
            {
                //  Stop early if:
                //  We were told to search for more time than we have left AND
                //  We now have less time than the low time threshold

                if ((currentTime < MinSearchTime) && ((playerTimeLeft - TimerBuffer) > MinSearchTime))
                {
                    //  If we ordinarily would stop, try enforcing a minimum search time
                    //  to prevent the time spent on moves from oscillating to a large degree.

                    //  As long as we have enough time left that this condition will be checked again,
                    //  postpone stopping until TotalSearchTime.Elapsed.TotalMilliseconds > MinSearchTime

                    Log("Postponed stopping! Only searched for " + currentTime + "ms of our " + (playerTimeLeft - TimerBuffer) + GetFormattedTime());
                }
                else
                {
                    Log("Stopping early! maxTime: " + MaxSearchTime + " >= playerTimeLeft: " + playerTimeLeft + " and we are in low time" + GetFormattedTime());

                    shouldStop = true;
                }

            }

            return shouldStop;
        }


        [MethodImpl(Inline)]
        private static string GetFormattedTime()
        {
            return ",\tcurrent time " + ((new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - debug_time_off).ToString("0000000"));
        }

        [MethodImpl(Inline)]
        public void MakeMoveTime(int ToMove, int moveCount)
        {
            int inc = PlayerIncrement[ToMove];
            int newSearchTime = PlayerIncrement[ToMove] + (PlayerTime[ToMove] / Math.Max(20, 20 - moveCount));

            this.MaxSearchTime = newSearchTime;
            Log("[INFO]: setting search time to " + (newSearchTime - inc) + " + " + inc + " = " + newSearchTime);
        }
    }
}