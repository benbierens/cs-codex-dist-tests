﻿using CodexPlugin.Hooks;
using Core;
using OverwatchTranscript;

namespace CodexPlugin.OverwatchSupport
{
    public class CodexTranscriptWriter : ICodexHooksProvider
    {
        private readonly ITranscriptWriter writer;
        private readonly CodexLogConverter converter;
        private readonly NameIdMap nameIdMap = new NameIdMap();

        public CodexTranscriptWriter(ITranscriptWriter transcriptWriter)
        {
            writer = transcriptWriter;
            converter = new CodexLogConverter(writer, nameIdMap);
        }

        public void Finalize(string outputFilepath)
        {
            writer.Write(outputFilepath);
        }

        public ICodexNodeHooks CreateHooks(string nodeName)
        {
            return new CodexNodeTranscriptWriter(writer, nameIdMap, nodeName);
        }

        public void IncludeFile(string filepath)
        {
            writer.IncludeArtifact(filepath);   
        }

        public void ProcessLogs(IDownloadedLog[] downloadedLogs)
        {
            foreach (var log in downloadedLogs)
            {
                writer.IncludeArtifact(log.GetFilepath());
                converter.ProcessLog(log);
            }
        }
    }

    public class CodexNodeTranscriptWriter : ICodexNodeHooks
    {
        private readonly ITranscriptWriter writer;
        private readonly NameIdMap nameIdMap;
        private readonly string name;
        private string peerId = string.Empty;

        public CodexNodeTranscriptWriter(ITranscriptWriter writer, NameIdMap nameIdMap, string name)
        {
            this.writer = writer;
            this.nameIdMap = nameIdMap;
            this.name = name;
        }

        public void OnNodeStarted(string peerId, string image)
        {
            this.peerId = peerId;
            nameIdMap.Add(name, peerId);
            WriteCodexEvent(e =>
            {
                e.NodeStarted = new NodeStartedEvent
                {
                    Name = name,
                    Image = image,
                    Args = string.Empty
                };
            });
        }

        public void OnNodeStopping()
        {
            WriteCodexEvent(e =>
            {
                e.NodeStopped = new NodeStoppedEvent
                { 
                    Name = name
                };
            });
        }

        public void OnFileDownloaded(ContentId cid)
        {
            WriteCodexEvent(e =>
            {
                e.FileDownloaded = new FileDownloadedEvent
                {
                    Cid = cid.Id
                };
            });
        }

        public void OnFileUploaded(ContentId cid)
        {
            WriteCodexEvent(e =>
            {
                e.FileUploaded = new FileUploadedEvent
                { 
                    Cid = cid.Id
                };
            });
        }

        private void WriteCodexEvent(Action<OverwatchCodexEvent> action)
        {
            if (string.IsNullOrEmpty(peerId)) throw new Exception("PeerId required");

            var e = new OverwatchCodexEvent
            {
                PeerId = peerId
            };
            action(e);

            writer.Add(DateTime.UtcNow, e);
        }
    }
}