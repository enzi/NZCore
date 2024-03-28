// using Unity.Collections;
// using Unity.Entities;
//
// namespace NZCore
// {
//     public partial struct SecondWorldSystem : ISystem
//     {
//         public void OnCreate(ref SystemState state)
//         {
//             var newWorld = new World("TestWorld2");
//             
//             var systems = new NativeList<SystemTypeIndex>(Allocator.Temp);
//
//             foreach (var systemIndex in TypeManager.GetSystemTypeIndices(WorldSystemFilterFlags.Default).AsArray())
//             {
//                 var t = TypeManager.GetSystemType(systemIndex);
//
//                 if (t is not { Namespace: not null })
//                 {
//                     continue;
//                 }
//
//                 if (!t.Namespace.StartsWith("Unity.Scenes"))
//                 {
//                     continue;
//                 }
//                 
//                 systems.Add(systemIndex);
//             }
//
//             DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(newWorld, systems);
//             ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(newWorld);
//         }
//     }
// }

