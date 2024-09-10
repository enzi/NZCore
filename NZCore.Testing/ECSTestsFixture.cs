// <copyright project="NZCore" file="ECSTestsFixture.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.LowLevel;

namespace NZCore
{
	/// <summary>
	/// Copied from the Entities package and slightly modified to enable default world creation and fixing a call to an internal method via reflection.
	/// </summary>
	public abstract class ECSTestsFixture
	{
		public bool CreateDefaultWorld;

		private bool jobsDebuggerWasEnabled;
		private PlayerLoopSystem previousPlayerLoop;
#nullable enable
		private World? previousWorld;
		private World? world;
#nullable disable

		protected World World => world!;
		protected WorldUnmanaged WorldUnmanaged => World!.Unmanaged;
		protected EntityManager Manager { get; private set; }
		protected EntityManager.EntityManagerDebug ManagerDebug { get; private set; }

		[SetUp]
		public virtual void Setup()
		{
			// unit tests preserve the current player loop to restore later, and start from a blank slate.
			previousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
			PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

			previousWorld = World.DefaultGameObjectInjectionWorld;
			world = World.DefaultGameObjectInjectionWorld = CreateDefaultWorld ? DefaultWorldInitialization.Initialize("Default Test World") : new World("Empty Test World");
			World.UpdateAllocatorEnableBlockFree = true;
			Manager = this.World.EntityManager;
			ManagerDebug = new EntityManager.EntityManagerDebug(this.Manager);

			// Many ECS tests will only pass if the Jobs Debugger enabled;
			// force it enabled for all tests, and restore the original value at teardown.
			jobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
			JobsUtility.JobDebuggerEnabled = true;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
			// In case entities journaling is initialized, clear it
			EntitiesJournaling.Clear();
#endif
		}

		[TearDown]
		public virtual void TearDown()
		{
			// Clean up systems before calling CheckInternalConsistency because we might have filters etc
			// holding on SharedComponentData making checks fail
			while (World.Systems.Count > 0)
			{
				World.DestroySystemManaged(this.World.Systems[0]);
			}

			ManagerDebug.CheckInternalConsistency();
			World.Dispose();
			World.DefaultGameObjectInjectionWorld = this.previousWorld!;

			JobsUtility.JobDebuggerEnabled = this.jobsDebuggerWasEnabled;

			PlayerLoop.SetPlayerLoop(this.previousPlayerLoop);
		}

		// calls JobUtility.ClearSystemIds() (internal method)
		private void JobUtility_ClearSystemIds() => typeof(JobsUtility).GetMethod("ClearSystemIds", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
	}
}