// <copyright project="NZCore" file="NZID.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Helper
{
    public struct NZIDGenerator
    {
        private long currentTick;
        private long lastTime;
        private int sequence;

        private readonly long generatorId;

        private readonly long maskTime;
        private readonly long maskGenerator;
        private readonly long maskSequence;

        private readonly int bitShiftTime;
        private readonly int bitShiftGenerator;

        public NZIDGenerator(int generatorId, DateTime epoch, byte timeBits = 41, byte generatorBits = 10, byte sequenceBits = 16)
        {
            currentTick = DateTime.UtcNow.Ticks;

            lastTime = -1;
            sequence = 0;
            this.generatorId = generatorId;

            maskTime = (1L << timeBits) - 1;
            maskGenerator = (1L << generatorBits) - 1;
            maskSequence = (1L << sequenceBits) - 1;

            bitShiftTime = generatorBits + sequenceBits;
            bitShiftGenerator = sequenceBits;
        }

        public void Update()
        {
            currentTick = DateTime.UtcNow.Ticks;
        }

        public long Create()
        {
            while (true)
            {
                currentTick = DateTime.UtcNow.Ticks;

                var timestamp = currentTick & maskTime;

                if (timestamp < lastTime || currentTick < 0)
                {
                    //exception = new InvalidSystemClockException($"Clock moved backwards or wrapped around. Refusing to generate id for {_lastgen - timestamp} ticks");
                    return -1;
                }

                if (timestamp == lastTime)
                {
                    if (sequence >= maskSequence)
                    {
                        //var localTick = currentTick;

                        // SpinWait.SpinUntil(() =>
                        // {
                        //     //Debug.Log($"Spin to win {localTick}/{DateTime.UtcNow.Ticks}");
                        //     return localTick != DateTime.UtcNow.Ticks;
                        // });

                        // while (Volatile.Read(ref localTick) == DateTime.UtcNow.Ticks)
                        // {
                        //     Debug.Log($"Spin to win {localTick}/{DateTime.UtcNow.Ticks}");
                        //     break;
                        // }

                        currentTick = DateTime.UtcNow.Ticks;
                        continue;
                    }

                    sequence++;
                }
                else
                {
                    sequence = 0;
                    lastTime = timestamp;
                }

                return (timestamp << bitShiftTime) | (generatorId << bitShiftGenerator) | (long)sequence;
            }
        }
    }
}