namespace VTStudioToolBox.Network;

public enum NatType
{
    Unknown,
    OpenInternet,
    FullCone,
    RestrictedCone,
    PortRestrictedCone,
    Symmetric,
    SymmetricUdpFirewall,
    UdpBlocked,
    UnsupportedServer
}

public enum MappingBehavior
{
    Unknown,
    UnsupportedServer,
    Direct,
    EndpointIndependent,
    AddressDependent,
    AddressAndPortDependent,
    Fail
}

public enum FilteringBehavior
{
    Unknown,
    UnsupportedServer,
    EndpointIndependent,
    AddressDependent,
    AddressAndPortDependent
}

public enum BindingTestResult
{
    Unknown,
    UnsupportedServer,
    Success,
    Fail
}
