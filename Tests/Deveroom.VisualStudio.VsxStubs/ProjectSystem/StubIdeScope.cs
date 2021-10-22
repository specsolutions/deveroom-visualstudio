﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using Deveroom.VisualStudio.Diagonostics;
using Deveroom.VisualStudio.Discovery;
using Deveroom.VisualStudio.Monitoring;
using Deveroom.VisualStudio.ProjectSystem;
using Deveroom.VisualStudio.ProjectSystem.Actions;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit.Abstractions;

namespace Deveroom.VisualStudio.VsxStubs.ProjectSystem
{
    public class StubIdeScope : IIdeScope
    {
        public StubLogger StubLogger { get; } = new StubLogger();
        public DeveroomCompositeLogger CompositeLogger { get; } = new DeveroomCompositeLogger
        {
            new DeveroomDebugLogger(TraceLevel.Verbose)
        };

        public bool IsSolutionLoaded { get; } = true;

        public IProjectScope GetProject(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetProperty<IProjectScope>(typeof(IProjectScope));
        }

        public IDeveroomLogger Logger => CompositeLogger;
        public IIdeActions Actions { get; set; }
        public IDeveroomWindowManager WindowManager => StubWindowManager;
        public IFileSystem FileSystem { get; } = new FileSystem();
        public IDeveroomOutputPaneServices DeveroomOutputPaneServices { get; } = null;
        public IDeveroomErrorListServices DeveroomErrorListServices { get; } = null;
        public StubWindowManager StubWindowManager { get; } = new StubWindowManager();
        public List<IProjectScope> ProjectScopes { get; } = new List<IProjectScope>();
        public IMonitoringService MonitoringService { get; }

        public event EventHandler<EventArgs> WeakProjectsBuilt;
        public event EventHandler<EventArgs> WeakProjectOutputsUpdated;

        public void CalculateSourceLocationTrackingPositions(IEnumerable<SourceLocation> sourceLocations)
        {
        }

        public IProjectScope[] GetProjectsWithFeatureFiles()
        {
            return ProjectScopes.ToArray();
        }

        public StubIdeScope(ITestOutputHelper testOutputHelper)
        {
            MonitoringService = new Mock<IMonitoringService>().Object;
            CompositeLogger.Add(new DeveroomXUnitLogger(testOutputHelper));
            CompositeLogger.Add(StubLogger);
            Actions = new StubIdeActions(this);
            VsxStubObjects.Initialize();
        }

        public void TriggerProjectsBuilt()
        {
            WeakProjectsBuilt?.Invoke(this, EventArgs.Empty);
            WeakProjectOutputsUpdated?.Invoke(this, EventArgs.Empty);
        }

        class ActionsSetter : IDisposable
        {
            private readonly StubIdeScope _ideScope;
            private readonly IIdeActions _originalActions;

            public ActionsSetter(StubIdeScope ideScope, IIdeActions actions)
            {
                _ideScope = ideScope;
                _originalActions = _ideScope.Actions;
                _ideScope.Actions = actions;
            }

            public void Dispose()
            {
                _ideScope.Actions = _originalActions;
            }
        }

        public IDisposable SetActions(IIdeActions actions)
        {
            return new ActionsSetter(this, actions);
        }
    }
}
