// <copyright project="NZCore" file="NZID.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.Helper
{
    public struct NzidGenerator
    {
        private long _currentTick;
        private long _lastTime;
        private int _sequence;

        private readonly long _generatorId;

        private readonly long _maskTime;
        private readonly long _maskGenerator;
        private readonly long _maskSequence;

        private readonly int _bitShiftTime;
        private readonly int _bitShiftGenerator;

        public NzidGenerator(int generatorId, DateTime epoch, byte timeBits = 41, byte generatorBits = 10, byte sequenceBits = 16)
        {
            _currentTick = DateTime.UtcNow.Ticks;

            _lastTime = -1;
            _sequence = 0;
            _generatorId = generatorId;

            _maskTime = (1L << timeBits) - 1;
            _maskGenerator = (1L << generatorBits) - 1;
            _maskSequence = (1L << sequenceBits) - 1;

            _bitShiftTime = generatorBits + sequenceBits;
            _bitShiftGenerator = sequenceBits;
        }

        public void Update()
        {
            _currentTick = DateTime.UtcNow.Ticks;
        }

        public long Create()
        {
            while (true)
            {
                _currentTick = DateTime.UtcNow.Ticks;

                var timestamp = _currentTick & _maskTime;

                if (timestamp < _lastTime || _currentTick < 0)
                {
                    //exception = new InvalidSystemClockException($"Clock moved backwards or wrapped around. Refusing to generate id for {_lastgen - timestamp} ticks");
                    return -1;
                }

                if (timestamp == _lastTime)
                {
                    if (_sequence >= _maskSequence)
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

                        _currentTick = DateTime.UtcNow.Ticks;
                        continue;
                    }

                    _sequence++;
                }
                else
                {
                    _sequence = 0;
                    _lastTime = timestamp;
                }

                return (timestamp << _bitShiftTime) | (_generatorId << _bitShiftGenerator) | (long)_sequence;
            }
        }
    }
}