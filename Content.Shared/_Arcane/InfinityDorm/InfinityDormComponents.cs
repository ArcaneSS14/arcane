// MIT License

// Copyright (c) 2026 Vecortys (vecortys@gmail.com)

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.InfinityDorm;

[RegisterComponent]
public sealed partial class InfinityDormComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Creator;

    [ViewVariables(VVAccess.ReadOnly)]
    public int Number;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ConnectedTeleporter;
}

[RegisterComponent]
public sealed partial class InfinityDormVisitorComponent : Component { }

[RegisterComponent]
public sealed partial class InfinityDormTeleporterComponent : Component
{
    [DataField]
    public string NeedAlertLevel = "green";
}

[RegisterComponent]
public sealed partial class InfinityDormExitComponent : Component
{
    [DataField]
    public TimeSpan TimeToExit = TimeSpan.FromSeconds(3);
}

[RegisterComponent]
public sealed partial class InfinityDormSpawnMarkerComponent : Component { }

[Serializable, NetSerializable]
public enum InfinityDormTeleporterUiKey
{
    Key
}
