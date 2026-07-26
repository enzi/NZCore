// <copyright project="Assembly-CSharp" file="LocalizedStringRefExtensions.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using AeonWeaver.Data;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace Localization.Authoring
{
	public static class LocalizedStringRefExtensions
	{
		public static LocalizedStringRef ConvertWithError(this LocalizedString localizedString, Object context)
		{
			if (localizedString == null)
			{
				Debug.LogError("LocalizedString is null!", context);
				return default;
			}

			localizedString.TableReference.OnAfterDeserialize();
			localizedString.TableEntryReference.OnAfterDeserialize();

			if (localizedString.TableReference.ReferenceType is TableReference.Type.Empty ||
			    localizedString.TableEntryReference.ReferenceType is TableEntryReference.Type.Empty)
			{
				Debug.LogError($"LocalizedString '{context.name}' is empty!", context);
				return default;
			}

			var entryReference = localizedString.TableEntryReference.ReferenceType is TableEntryReference.Type.Name
				? localizedString.TableReference.SharedTableData.GetEntry(localizedString.TableEntryReference.Key).Id
				: localizedString.TableEntryReference.KeyId;

			var localizedStringRef = new LocalizedStringRef
			{
				TableReference = localizedString.TableReference.SharedTableData
					? localizedString.TableReference.SharedTableData.TableCollectionNameGuid
					: Guid.Empty,
				EntryReference = entryReference
			};

			if (localizedStringRef.EntryReference == 0 || localizedStringRef.TableReference == Guid.Empty)
			{
				throw new Exception("LocalizedString is not valid!");
			}

			return localizedStringRef;
		}

		public static LocalizedStringRef ConvertWithoutError(this LocalizedString localizedString, Object context)
		{
			if (localizedString == null)
			{
				return default;
			}

			localizedString.TableReference.OnAfterDeserialize();
			localizedString.TableEntryReference.OnAfterDeserialize();

			if (localizedString.TableReference.ReferenceType is TableReference.Type.Empty ||
			    localizedString.TableEntryReference.ReferenceType is TableEntryReference.Type.Empty)
			{
				return default;
			}

			var entryReference = localizedString.TableEntryReference.ReferenceType is TableEntryReference.Type.Name
				? localizedString.TableReference.SharedTableData.GetEntry(localizedString.TableEntryReference.Key).Id
				: localizedString.TableEntryReference.KeyId;

			return new LocalizedStringRef
			{
				TableReference = localizedString.TableReference.SharedTableData
					? localizedString.TableReference.SharedTableData.TableCollectionNameGuid
					: Guid.Empty,
				EntryReference = entryReference
			};
		}
	}
}