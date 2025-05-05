// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Vanara.PInvoke;
using Windows.ApplicationModel.Background;
using WinRT;
using static Vanara.PInvoke.Ole32;

namespace BackgroundTaskBuilder;

// COM server startup code.
static partial class ComServer
{
	internal sealed partial class BackgroundTaskFactory : IClassFactory
	{
		public HRESULT CreateInstance(object? objectAsUnknown, in Guid interfaceId, out object? objectPointer)
		{
			if (objectAsUnknown != null)
			{
				objectPointer = null;
				return HRESULT.CLASS_E_NOAGGREGATION;
			}

			if ((interfaceId != typeof(BackgroundTask).GUID) && (interfaceId != IID_IUnknown))
			{
				objectPointer = null;
				return HRESULT.E_NOINTERFACE;
			}

			objectPointer = new BackgroundTask().As<IBackgroundTask>();
			return HRESULT.S_OK;
		}

		public HRESULT LockServer(bool fLock) => HRESULT.S_OK;
	}
}
