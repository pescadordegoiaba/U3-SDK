////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace SDG.Unturned.LinuxPerformance
{
	public struct NativeContextHandle : IEquatable<NativeContextHandle>
	{
		public readonly IntPtr Value;
		public bool IsValid => Value != IntPtr.Zero;

		public NativeContextHandle(IntPtr value)
		{
			Value = value;
		}

		public bool Equals(NativeContextHandle other) => Value == other.Value;
		public override bool Equals(object obj) => obj is NativeContextHandle other && Equals(other);
		public override int GetHashCode() => Value.GetHashCode();
	}
}
