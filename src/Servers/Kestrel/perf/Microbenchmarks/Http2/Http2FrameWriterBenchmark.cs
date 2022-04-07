// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2.FlowControl;
using Microsoft.AspNetCore.Testing;

namespace Microsoft.AspNetCore.Server.Kestrel.Microbenchmarks;

public class Http2FrameWriterBenchmark
{
    private MemoryPool<byte> _memoryPool;
    private Pipe _pipe;
    private Http2FrameWriter _frameWriter;
    private HttpResponseHeaders _responseHeaders;
    private Http2Stream _stream;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _memoryPool = PinnedBlockMemoryPoolFactory.Create();

        var options = new PipeOptions(_memoryPool, readerScheduler: PipeScheduler.Inline, writerScheduler: PipeScheduler.Inline, useSynchronizationContext: false);
        _pipe = new Pipe(options);

        var serviceContext = TestContextFactory.CreateServiceContext(
            serverOptions: new KestrelServerOptions(),
            httpParser: new HttpParser<Http1ParsingHandler>(),
            dateHeaderValueManager: new DateHeaderValueManager());

        _frameWriter = new Http2FrameWriter(
            new NullPipeWriter(),
            connectionContext: null,
            http2Connection: null,
            new OutputFlowControl(new SingleAwaitableProvider(), initialWindowSize: int.MaxValue),
            timeoutControl: null,
            minResponseDataRate: null,
            "TestConnectionId",
            _memoryPool,
            serviceContext);

        _stream = new MockHttp2Stream(TestContextFactory.CreateHttp2StreamContext(streamId: 0));

        _responseHeaders = new HttpResponseHeaders();
        var headers = (IHeaderDictionary)_responseHeaders;
        headers.ContentType = "application/json";
        headers.ContentLength = 1024;
    }

    [Benchmark]
    public void WriteResponseHeaders()
    {
        _frameWriter.WriteResponseHeaders(_stream, 200, endStream: true, _responseHeaders);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _pipe.Writer.Complete();
        _memoryPool?.Dispose();
    }

    private class MockHttp2Stream : Http2Stream
    {
        public MockHttp2Stream(Http2StreamContext context)
        {
            Initialize(context);
        }

        public override void Execute()
        {
        }
    }
}