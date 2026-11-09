// <copyright project="NZCore.Tests" file="ScratchSceneSetUp.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using NUnit.Framework;

namespace NZCore.Tests.AssetManagement
{
    [SetUpFixture]
    public class ScratchSceneSetUp : ScratchSceneSetUpBase
    {
        public ScratchSceneSetUp()
            : base("NZCoreTestsScratch")
        {
        }
    }
}
