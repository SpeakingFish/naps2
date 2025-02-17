﻿using NAPS2.Remoting.Worker;

namespace NAPS2.Scan.Internal;

internal class ScanBridgeFactory : IScanBridgeFactory
{
    private readonly ScanningContext _scanningContext;

    public ScanBridgeFactory(ScanningContext scanningContext)
    {
        _scanningContext = scanningContext;
    }

    public IScanBridge Create(ScanOptions options)
    {
        // TODO: Ideally Apple could be run in a worker too for stability. But we would need to set up the worker with
        // an NSApplication etc. (I assume - trying without gives a Device Offline error)
        if (options.Driver == Driver.Sane)
        {
            // Run SANE in a worker process for added stability
            return new WorkerScanBridge(_scanningContext, WorkerType.Native);
        }
        if (options is { Driver: Driver.Twain, TwainOptions.Adapter: TwainAdapter.Legacy })
        {
            // Legacy twain needs to run in a 32-bit worker
            // (Normal twain also does, but it runs the worker at a lower level via RemoteTwainSessionController)
            return new WorkerScanBridge(_scanningContext, WorkerType.WinX86);
        }
        return new InProcScanBridge(_scanningContext);
    }
}