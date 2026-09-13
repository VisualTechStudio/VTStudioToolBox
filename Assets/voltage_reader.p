#include <pawnio.inc>
#include <native.inc>

// Read CPU voltage via MSR
// For Intel: MSR_PERF_STATUS (0x198) contains voltage info
// For AMD: Different MSR

DEFINE_IOCTL_SIZED(ioctl_read_voltage, 0, 1) {
    new cpu_vendor = get_cpu_vendor();
    
    if (cpu_vendor == CpuVendor_Intel) {
        return read_intel_voltage(out);
    } else if (cpu_vendor == CpuVendor_AMD || cpu_vendor == CpuVendor_Hygon) {
        return read_amd_voltage(out);
    }
    
    return STATUS_NOT_SUPPORTED;
}

NTSTATUS:read_intel_voltage(out[]) {
    // Intel MSR_PERF_STATUS (0x198)
    // Bits 0:15 = voltage in 16.16 fixed point (for older CPUs)
    // Or bits 0:7 = voltage in 8.8 format (for newer CPUs)
    
    new value;
    new NTSTATUS:status = msr_read(0x198, value);
    if (!NT_SUCCESS(status)) {
        // Try alternative: MSR_PERF_CTL (0x199)
        status = msr_read(0x199, value);
    }
    
    if (NT_SUCCESS(status)) {
        out[0] = value;
        return STATUS_SUCCESS;
    }
    
    return status;
}

NTSTATUS:read_amd_voltage(out[]) {
    // AMD: Try different MSRs
    // MSR_PWR_UNIT (0xC0010299) - Power reporting
    // MSR_CORE_ENERGY_STAT (0xC001029A) - Core energy status
    
    new value;
    new NTSTATUS:status;
    
    // Try MSR_CSTATE_CONFIG (0xC0010296) which may have voltage info
    status = msr_read(0xC0010296, value);
    if (NT_SUCCESS(status)) {
        out[0] = value;
        return STATUS_SUCCESS;
    }
    
    // Try MSR_PWR_UNIT
    status = msr_read(0xC0010299, value);
    if (NT_SUCCESS(status)) {
        out[0] = value;
        return STATUS_SUCCESS;
    }
    
    return STATUS_NOT_SUPPORTED;
}
