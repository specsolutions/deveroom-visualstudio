using System;
using System.Collections.Generic;
using System.Linq;
using Deveroom.VisualStudio.Diagonostics;
using Deveroom.VisualStudio.Editor.Services;
using Deveroom.VisualStudio.Monitoring;
using Deveroom.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Deveroom.VisualStudio.Editor.Commands.Infrastructure
{
    public abstract class DeveroomEditorCommandBase : IDeveroomEditorCommand
    {
        protected readonly IIdeScope IdeScope;
        protected readonly IBufferTagAggregatorFactoryService AggregatorFactory;
        protected readonly IMonitoringService MonitoringService;

        public virtual DeveroomEditorCommandTargetKey Target 
            => throw new NotImplementedException();

        public virtual DeveroomEditorCommandTargetKey[] Targets 
            => new [] { Target };

        protected IDeveroomLogger Logger => IdeScope.Logger;

        protected DeveroomEditorCommandBase(IIdeScope ideScope, IBufferTagAggregatorFactoryService aggregatorFactory, IMonitoringService monitoringService)
        {
            IdeScope = ideScope;
            AggregatorFactory = aggregatorFactory;
            MonitoringService = monitoringService;
        }

        protected DeveroomTag GetDeveroomTagForCaret(IWpfTextView textView, params string[] tagTypes)
        {
            var tag = DumpDeveroomTags(GetDeveroomTagsForCaret(textView, false)).FirstOrDefault(t => tagTypes.Contains(t.Type));
            if (tag != null &&
                tag.Span.Snapshot.Version.VersionNumber != textView.TextSnapshot.Version.VersionNumber)
            {
                Logger.LogWarning("Snapshot version mismatch");
                tag = DumpDeveroomTags(GetDeveroomTagsForCaret(textView, true)).FirstOrDefault(t => tagTypes.Contains(t.Type));
            }
            return tag;
        }

        protected IEnumerable<DeveroomTag> GetDeveroomTagsForCaret(IWpfTextView textView, bool forceUpToDate)
        {
            var tagger = DeveroomTaggerProvider.GetDeveroomTagger(textView.TextBuffer);
            if (tagger != null)
            {
                var spans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(textView.Caret.Position.BufferPosition, 0));
                return tagger.GetTags(spans, forceUpToDate).Select(t => t.Tag);
            }
            return Enumerable.Empty<DeveroomTag>();
        }

        protected IEnumerable<DeveroomTag> DumpDeveroomTags(IEnumerable<DeveroomTag> deveroomTags)
        {
            return deveroomTags;
            //foreach (var deveroomTag in deveroomTags)
            //{
            //    Logger.LogVerbose($"  Tag: {deveroomTag.Type}");
            //    yield return deveroomTag;
            //}
        }

        protected string GetEditorDocumentPath(IWpfTextView textView)
        {
            if (!textView.TextBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer bufferAdapter))
                return null;

            if (!(bufferAdapter is IPersistFileFormat persistFileFormat))
                return null;

            if (!ErrorHandler.Succeeded(persistFileFormat.GetCurFile(out string filePath, out _)))
                return null;

            return filePath;
        }

        public virtual DeveroomEditorCommandStatus QueryStatus(IWpfTextView textView, DeveroomEditorCommandTargetKey commandKey)
        {
            return DeveroomEditorCommandStatus.Supported;
        }

        public virtual bool PreExec(IWpfTextView textView, DeveroomEditorCommandTargetKey commandKey, IntPtr inArgs = default(IntPtr))
        {
            return false;
        }

        public virtual bool PostExec(IWpfTextView textView, DeveroomEditorCommandTargetKey commandKey, IntPtr inArgs = default(IntPtr))
        {
            return false;
        }

        #region Helper methods
        protected void SetSelectionToChangedLines(IWpfTextView textView, ITextSnapshotLine[] lines)
        {
            var newSnapshot = textView.TextBuffer.CurrentSnapshot;
            var selectionStartPosition = newSnapshot.GetLineFromLineNumber(lines.First().LineNumber).Start;
            var selectionEndPosition = newSnapshot.GetLineFromLineNumber(lines.Last().LineNumber).End;
            textView.Selection.Select(new SnapshotSpan(
                selectionStartPosition,
                selectionEndPosition), false);
            textView.Caret.MoveTo(selectionEndPosition);
        }

        protected SnapshotSpan GetSelectionSpan(IWpfTextView textView)
        {
            return new SnapshotSpan(textView.Selection.Start.Position, textView.Selection.End.Position);
        }

        protected IEnumerable<ITextSnapshotLine> GetSpanFullLines(SnapshotSpan span)
        {
            var selectionStartLine = span.Start.GetContainingLine();
            var selectionEndLine = GetSelectionEndLine(selectionStartLine, span);
            for (int lineNumber = selectionStartLine.LineNumber; lineNumber <= selectionEndLine.LineNumber; lineNumber++)
            {
                yield return selectionStartLine.Snapshot.GetLineFromLineNumber(lineNumber);
            }
        }

        private ITextSnapshotLine GetSelectionEndLine(ITextSnapshotLine selectionStartLine, SnapshotSpan span)
        {
            var selectionEndLine = span.End.GetContainingLine();
            // if the selection ends exactly at the beginning of a new line (ie line select), we do not comment out the last line
            if (selectionStartLine.LineNumber != selectionEndLine.LineNumber && selectionEndLine.Start.Equals(span.End))
            {
                selectionEndLine = selectionEndLine.Snapshot.GetLineFromLineNumber(selectionEndLine.LineNumber - 1);
            }
            return selectionEndLine;
        }
        #endregion
    }
}