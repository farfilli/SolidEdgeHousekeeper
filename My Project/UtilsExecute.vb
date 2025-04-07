﻿Option Strict On

Public Class UtilsExecute

    Public Property FMain As Form_Main
    Public Property FilesToProcessTotal As Integer
    Public Property FilesToProcessCompleted As Integer
    'Public Property UtilsLogFile As UtilsLogFile
    Public Property TotalAborts As Double
    Public Property TotalAbortsMaximum As Integer = 4
    Public Property SEApp As SolidEdgeFramework.Application
    Public Property StartTime As DateTime
    Public Property TextBoxStatus As TextBox
    Public Property ErrorLogger As HCErrorLogger

    Public Sub New(_Form_Main As Form_Main)
        Me.FMain = _Form_Main
    End Sub

    Public Sub ProcessAll()

        FMain.Cursor = Cursors.WaitCursor

        Dim ElapsedTime As Double
        Dim ElapsedTimeText As String

        Me.ErrorLogger = New HCErrorLogger

        FMain.SaveSettings(SavingPresets:=False)  ' Updates JSON Properties and saves settings

        If Not CheckStartConditions() Then
            FMain.Cursor = Cursors.Default
            Exit Sub
        End If

        Dim UFL As New UtilsFileList(FMain, FMain.ListViewFiles, FMain.ListViewSources)

        FilesToProcessTotal = UFL.GetTotalFilesToProcess()
        FilesToProcessCompleted = 0

        FMain.StopProcess = False
        FMain.ButtonCancel.Text = "Stop"

        OleMessageFilter.Register()

        TotalAborts = 0

        Dim USEA = New UtilsSEApp
        USEA.TextBoxStatus = Me.TextBoxStatus

        If FMain.SolidEdgeRequired > 0 Then
            USEA.SEStart(FMain.RunInBackground, FMain.UseCurrentSession, FMain.NoUpdateMRU)
            SEApp = USEA.SEApp
        End If

        Me.StartTime = Now

        Dim PartCount As Integer = 0
        Dim SheetmetalCount As Integer = 0
        Dim AssemblyCount As Integer = 0
        Dim DraftCount As Integer = 0

        For Each Task As Task In FMain.TaskList
            If Task.IsSelectedTask And Task.IsSelectedPart Then PartCount += 1
            If Task.IsSelectedTask And Task.IsSelectedSheetmetal Then SheetmetalCount += 1
            If Task.IsSelectedTask And Task.IsSelectedAssembly Then AssemblyCount += 1
            If Task.IsSelectedTask And Task.IsSelectedDraft Then DraftCount += 1

            Task.ErrorLogger = Me.ErrorLogger
        Next

        ' Process the files
        If PartCount > 0 Then ProcessFiles("Part")
        If SheetmetalCount > 0 Then ProcessFiles("Sheetmetal")
        If AssemblyCount > 0 Then ProcessFiles("Assembly")
        If DraftCount > 0 Then ProcessFiles("Draft")

        If FMain.SolidEdgeRequired > 0 Then
            USEA.SEStop(FMain.UseCurrentSession)
            SEApp = Nothing
        End If

        OleMessageFilter.Revoke()

        If FMain.StopProcess Then
            If TotalAborts > TotalAbortsMaximum Then
                FMain.TextBoxStatus.Text = "The number of file processing errors exceeded maximum.  Stopping."
            Else
                FMain.TextBoxStatus.Text = "Processing halted by user."
            End If
        Else
            ElapsedTime = Now.Subtract(StartTime).TotalMinutes
            If ElapsedTime < 60 Then
                ElapsedTimeText = "in " + ElapsedTime.ToString("0.0") + " min."
            Else
                ElapsedTimeText = "in " + (ElapsedTime / 60).ToString("0.0") + " hr."
            End If

            FMain.TextBoxStatus.Text = "Finished processing " + FilesToProcessTotal.ToString + " files " + ElapsedTimeText
        End If

        FMain.LabelTimeRemaining.Text = ""

        FMain.StopProcess = False
        FMain.ButtonCancel.Text = "Cancel"

        If Me.ErrorLogger.HasErrors Then
            Me.ErrorLogger.Save()
            Try
                ' Try to use the default application to open the file.
                Process.Start(Me.ErrorLogger.LogfileName)
            Catch ex As Exception
                ' If none, open with notepad.exe
                Process.Start("notepad.exe", Me.ErrorLogger.LogfileName)
            End Try
        Else
            FMain.TextBoxStatus.Text = FMain.TextBoxStatus.Text + "  All checks passed."
        End If

        FMain.Cursor = Cursors.Default

    End Sub

    Public Function CheckStartConditions() As Boolean
        Dim Proceed As Boolean = True

        Dim ErrorLogger As New HCErrorLogger
        Dim HeaderLogger As Logger = ErrorLogger.AddFile("Please correct the following before continuing")
        Dim StartLogger As Logger = HeaderLogger.AddLogger("")

        Dim USEA = New UtilsSEApp
        USEA.TextBoxStatus = Me.TextBoxStatus

        If Not FMain.UseCurrentSession Then
            If USEA.SEIsRunning() Then
                StartLogger.AddMessage("Close Solid Edge")
            End If
        End If

        If USEA.DMIsRunning() Then
            StartLogger.AddMessage("Close Revision Manager")
        End If

        If FMain.ListViewFilesOutOfDate Then
            StartLogger.AddMessage("Update the file list (Orange button on the Home Tab toolstrip)")
        End If

        If FMain.TLABottomUp Then
            If Not FileIO.FileSystem.FileExists(FMain.FastSearchScopeFilename) Then
                StartLogger.AddMessage("Enter a valid Fast Search Scope file (on the Configuration Tab - Top Level Assembly Page)")
            End If
        End If

        FMain.ListViewFiles.BeginUpdate()

        For Each Filename As ListViewItem In FMain.ListViewFiles.Items 'L-istBoxFiles.Items

            FMain.TextBoxStatus.Text = String.Format("Checking file integrity: {0}", IO.Path.GetFileName(Filename.Name))
            Application.DoEvents()

            If Filename.Group.Name <> "Sources" Then

                Filename.ImageKey = "Unchecked"

                If Not FileIO.FileSystem.FileExists(Filename.Name) Then
                    StartLogger.AddMessage("File not found, or Path exceeds maximum length")
                    StartLogger.AddMessage(CType(Filename.Name, String))
                    FMain.ListViewFilesOutOfDate = True
                    Exit For
                End If
            End If
        Next

        FMain.ListViewFiles.EndUpdate()

        If FMain.ListViewFiles.Items.Count = 0 Then
            StartLogger.AddMessage("Select an input directory with files to process")
        End If

        If FMain.EnableFileWildcard Then
            If FMain.FileWildcard = "" Then
                StartLogger.AddMessage("Enter a file wildcard search string")
            End If
        End If

        FMain.SolidEdgeRequired = 0
        Dim SelectedTasksCount As Integer = 0
        Dim IncompatibleTaskNamesList As New List(Of String)

        For Each Task As Task In FMain.TaskList
            If Task.IsSelectedTask Then
                SelectedTasksCount += 1

                If Task.RequiresSourceDirectories Then
                    Dim UFL As New UtilsFileList(FMain, FMain.ListViewFiles, FMain.ListViewSources)
                    Task.SourceDirectories = UFL.GetSourceDirectories()
                End If

                ' True returns -1 upon conversion
                FMain.SolidEdgeRequired -= CType(Task.SolidEdgeRequired, Integer)

                If Not Task.CompatibleWithOtherTasks Then
                    IncompatibleTaskNamesList.Add(Task.Description)
                End If

                Dim SubLogger As Logger = StartLogger.AddLogger(Task.Description)
                Task.CheckStartConditions(SubLogger)

            End If
        Next

        If FMain.SolidEdgeRequired <> 0 Then
            If SelectedTasksCount <> FMain.SolidEdgeRequired Then
                StartLogger.AddMessage("Cannot run some Tasks with SE and others without")
            End If
        End If

        If (IncompatibleTaskNamesList.Count > 0) And (Not SelectedTasksCount = 1) Then
            For Each s As String In IncompatibleTaskNamesList
                StartLogger.AddMessage(String.Format("{0} cannot be run with other tasks enabled", s))
            Next
        End If

        If SelectedTasksCount = 0 Then
            StartLogger.AddMessage("Select at least one task to perform")
        End If

        Try
            Dim i = CInt(FMain.ListViewUpdateFrequency)
            If i <= 0 Then
                FMain.ListViewUpdateFrequency = "1"
            End If
        Catch ex As Exception
            FMain.ListViewUpdateFrequency = "1"
        End Try

        FMain.TextBoxStatus.Text = ""

        Dim Outlist As List(Of String) = ErrorLogger.FormatReport()

        If Outlist.Count > 0 Then
            Proceed = False

            Dim Outstring As String = ""
            For Each s As String In Outlist
                Outstring = String.Format("{0}{1}{2}", Outstring, s, vbCrLf)
            Next

            MsgBox(Outstring, vbOKOnly)

        End If

        Return Proceed
    End Function

    Public Sub UpdateTimeRemaining()
        Dim ElapsedTime As Double
        Dim RemainingTime As Double
        Dim TotalEstimatedTime As Double
        Dim ElapsedTimeString As String
        Dim RemainingTimeString As String

        If FilesToProcessCompleted > 2 Then
            ElapsedTime = Now.Subtract(StartTime).TotalMinutes

            TotalEstimatedTime = ElapsedTime * CDbl(FilesToProcessTotal) / CDbl(FilesToProcessCompleted)
            RemainingTime = TotalEstimatedTime - ElapsedTime

            If ElapsedTime < 60 Then
                ElapsedTimeString = String.Format("{0} m", ElapsedTime.ToString("0.0"))
            Else
                ElapsedTimeString = String.Format("{0} h", (ElapsedTime / 60).ToString("0.0"))
            End If

            If RemainingTime < 60 Then
                RemainingTimeString = String.Format("{0} m", RemainingTime.ToString("0.0"))
            Else
                RemainingTimeString = String.Format("{0} h", (RemainingTime / 60).ToString("0.0"))
            End If

            If RemainingTime < 0.1 Then
                FMain.LabelTimeRemaining.Text = ""
            Else
                FMain.LabelTimeRemaining.Text = String.Format("Elapsed {0}, Remaining {1}", ElapsedTimeString, RemainingTimeString)
            End If


        End If
    End Sub

    Public Sub ProcessFiles(ByVal Filetype As String)

        Dim FilesToProcess As List(Of String)
        Dim FileToProcess As String
        Dim msg As String

        Dim UFL As New UtilsFileList(FMain, FMain.ListViewFiles, FMain.ListViewSources)

        If Filetype = "Assembly" Then
            FilesToProcess = UFL.GetFileNames("*.asm")
        ElseIf Filetype = "Part" Then
            FilesToProcess = UFL.GetFileNames("*.par")
        ElseIf Filetype = "Sheetmetal" Then
            FilesToProcess = UFL.GetFileNames("*.psm")
        ElseIf Filetype = "Draft" Then
            FilesToProcess = UFL.GetFileNames("*.dft")
        Else
            MsgBox("In ProcessFiles(), Filetype not recognized: " + Filetype + ".  Exiting...")
            SEApp.Quit()
            End
        End If

        Dim idx As Integer = 0

        Dim LVItemReverseLUT As New List(Of ListViewItem)
        For Each tmpItem As ListViewItem In FMain.ListViewFiles.Items
            If FilesToProcess.Contains(tmpItem.Name) Then
                LVItemReverseLUT.Add(tmpItem)
                idx += 1
            End If
        Next

        idx = 0
        For Each FileToProcess In FilesToProcess

            If (FilesToProcessCompleted > 0) And (FilesToProcessCompleted Mod CInt(FMain.ListViewUpdateFrequency) = 0) Then
                LVItemReverseLUT(idx).EnsureVisible()

            End If

            System.Windows.Forms.Application.DoEvents()
            If FMain.StopProcess Then
                FMain.TextBoxStatus.Text = "Processing aborted"
                Exit Sub
            End If

            ProcessFile(FileToProcess, Filetype)

            If Me.ErrorLogger.FileLoggerHasErrors(FileToProcess) Then
                Dim tmpPath As String = System.IO.Path.GetDirectoryName(FileToProcess)
                Dim tmpFilename As String = System.IO.Path.GetFileName(FileToProcess)
                Dim s As String = String.Format("{0} in {1}", tmpFilename, tmpPath)

                LVItemReverseLUT(idx).ImageKey = "Error"
            Else
                LVItemReverseLUT(idx).ImageKey = "Checked"
            End If

            FilesToProcessCompleted += 1  ' This is for all files in the run
            idx += 1  ' This is for files in this loop

            msg = FilesToProcessCompleted.ToString + "/" + FilesToProcessTotal.ToString + " "
            msg += System.IO.Path.GetFileName(FileToProcess)
            FMain.TextBoxStatus.Text = msg

        Next

    End Sub

    Public Sub ProcessFile(
        ByVal Path As String,
        ByVal Filetype As String
        ) 'As Dictionary(Of String, List(Of String))

        'Dim ErrorMessage As New Dictionary(Of Integer, List(Of String))
        'Dim ExitStatus As Integer
        'Dim ErrorMessagesCombined As New Dictionary(Of String, List(Of String))

        Dim FileLogger As Logger = Me.ErrorLogger.AddFile(Path)

        Dim SEDoc As SolidEdgeFramework.SolidEdgeDocument = Nothing

        Dim ActiveWindow As SolidEdgeFramework.Window
        Dim ActiveSheetWindow As SolidEdgeDraft.SheetWindow

        Dim UC As New UtilsCommon

        Dim tf As Boolean

        Dim Proceed As Boolean = True


        '###### EXCEPTION ACCOUNTING ######

        TotalAborts -= 0.1  ' Discounting for infrequent malfunctions on a large number of files.
        If TotalAborts < 0 Then
            TotalAborts = 0
        End If


        ' ###### DOCUMENT STATUS ######

        Dim OldStatus As String = ""

        If FMain.ProcessAsAvailable And FMain.SolidEdgeRequired > 0 Then

            Dim s As String = SetStartingStatus(OldStatus, Path) 'SetStartingStatus populates OldStatus
            If Not s = "" Then
                'ErrorMessagesCombined(s) = New List(Of String) From {""}
                FileLogger.AddMessage(s)
            End If

        End If


        '###### PROCESS ######

        Try
            '###### OPEN FILE ######

            If FMain.SolidEdgeRequired > 0 Then

                Proceed = CheckVersion(SEApp, Path)
                If Not Proceed Then
                    'ErrorMessagesCombined("Error opening file") = New List(Of String) From {""}
                    FileLogger.AddMessage("Error opening file")
                End If

                If Proceed Then
                    If (FMain.RunInBackground) And (Not Filetype = "Assembly") Then
                        'SEDoc = SolidEdgeCommunity.Extensions.DocumentsExtensions.OpenInBackground(
                        '            Of SolidEdgeFramework.SolidEdgeDocument)(SEApp.Documents, Path)

                        ' Here is the same functionality without using the SolidEdgeCommunity dependency
                        ' https://blogs.sw.siemens.com/solidedge/how-to-open-documents-silently/
                        Dim JDOCUMENTPROP_NOWINDOW As UInt16 = 8
                        SEDoc = DirectCast(SEApp.Documents.Open(Path, JDOCUMENTPROP_NOWINDOW), SolidEdgeFramework.SolidEdgeDocument)

                    Else
                        SEDoc = DirectCast(SEApp.Documents.Open(Path), SolidEdgeFramework.SolidEdgeDocument)
                        SEDoc.Activate()

                        ' Maximize the window in the application
                        If Filetype = "Draft" Then
                            ActiveSheetWindow = CType(SEApp.ActiveWindow, SolidEdgeDraft.SheetWindow)
                            ActiveSheetWindow.WindowState = 2
                        Else
                            ActiveWindow = CType(SEApp.ActiveWindow, SolidEdgeFramework.Window)
                            ActiveWindow.WindowState = 2  '0 normal, 1 minimized, 2 maximized
                        End If
                    End If

                    SEApp.DoIdle()

                End If
            End If


            '###### PERFORM TASKS ######

            If Proceed Then
                For Each Task As Task In FMain.TaskList
                    If Task.IsSelectedTask Then

                        Task.FileLogger = FileLogger

                        tf = (Filetype = "Assembly") And (Task.IsSelectedAssembly)
                        tf = tf Or ((Filetype = "Part") And (Task.IsSelectedPart))
                        tf = tf Or ((Filetype = "Sheetmetal") And (Task.IsSelectedSheetmetal))
                        tf = tf Or ((Filetype = "Draft") And (Task.IsSelectedDraft))

                        If tf Then

                            If FMain.SolidEdgeRequired > 0 Then
                                'ErrorMessage = Task.Process(SEDoc, FMain.Configuration, SEApp)
                                Task.Process(SEDoc, SEApp)
                            Else
                                'ErrorMessage = Task.Process(Path)
                                Task.Process(Path)
                            End If

                            'ExitStatus = ErrorMessage.Keys(0)

                            'If ExitStatus <> 0 Then
                            '    ErrorMessagesCombined(Task.Description) = ErrorMessage(ErrorMessage.Keys(0))
                            'End If

                            'If ExitStatus = 99 Or Me.ErrorLogger.Abort Then
                            '    FMain.StopProcess = True
                            'End If
                            If Me.ErrorLogger.Abort Then
                                FMain.StopProcess = True
                            End If

                        End If
                    End If
                Next

            End If


            '###### CLOSE FILE ######

            If FMain.SolidEdgeRequired > 0 Then
                If Proceed Then
                    SEDoc.Close(False)
                    SEApp.DoIdle()
                End If


                '###### DOCUMENT STATUS ######

                If FMain.ProcessAsAvailable Then
                    Dim s As String = SetEndingStatus(OldStatus, Path)
                    If Not s = "" Then
                        'ErrorMessagesCombined(s) = New List(Of String) From {""}
                        FileLogger.AddMessage(s)
                    End If
                End If

            End If

        Catch ex As Exception


            '###### EXCEPTION PROCESSING ######

            Dim AbortList As New List(Of String)

            AbortList.Add(ex.ToString)

            TotalAborts += 1
            If TotalAborts >= TotalAbortsMaximum Then
                FMain.StopProcess = True
                AbortList.Add(String.Format("Total aborts exceed maximum of {0}.  Exiting...", TotalAbortsMaximum))
            Else
                If FMain.SolidEdgeRequired > 0 Then
                    Dim USEA = New UtilsSEApp
                    USEA.TextBoxStatus = Me.TextBoxStatus

                    USEA.SEStop(FMain.UseCurrentSession)
                    SEApp = Nothing

                    USEA.SEStart(FMain.RunInBackground, FMain.UseCurrentSession, FMain.NoUpdateMRU)
                    SEApp = USEA.SEApp
                End If
            End If

            If ex.ToString.Contains("SolidEdgeFramework.Documents.Open") Then
                'ErrorMessagesCombined("Error opening file") = New List(Of String) From {""}
                FileLogger.AddMessage("Error opening file")
            Else
                'ErrorMessagesCombined("Error processing file") = AbortList
                FileLogger.AddMessage(String.Format("Error processing file"))
                For Each s In AbortList
                    FileLogger.AddMessage(s)
                Next
            End If
        End Try

        UpdateTimeRemaining()

        'Return ErrorMessagesCombined
    End Sub

    Private Function CheckVersion(SEApp As SolidEdgeFramework.Application, Filename As String) As Boolean
        ' Checks if the file to be processed is of the same or earlier version than the version installed on the machine.
        ' On at least some corrupted files, generates an exception which is interpreted as not possible to open
        ' Don't yet have a way to check Commercial, Academic, Community

        Dim IsOK As Boolean = True

        Dim Version = SEApp.Version
        Dim InstalledMajorVersion As Integer = CInt(Version.Split(CChar("."))(0))

        Dim DType As SolidEdgeFramework.DocumentTypeConstants = Nothing
        Dim CreatedVersion As String = ""
        Dim LastSavedVersion As String = ""
        Dim GeometricVersion As UInteger = 0
        Try
            SEApp.SEGetFileVersionInfo(Filename, DType, CreatedVersion, LastSavedVersion, GeometricVersion)
        Catch ex As Exception
            IsOK = False  'Corrupted file or other issue.
        End Try

        If IsOK Then
            Dim LastSavedMajorVersion As Integer = CInt(LastSavedVersion.Split(CChar("."))(0))
            If LastSavedMajorVersion > InstalledMajorVersion Then
                IsOK = False
            End If
        End If

        Return IsOK
    End Function

    Private Function SetStartingStatus(
        ByRef OldStatus As String,
        Path As String) As String

        Dim ErrorMessage As String = ""
        Dim Proceed As Boolean = True

        Dim SSDoc As HCStructuredStorageDoc = Nothing

        Try
            SSDoc = New HCStructuredStorageDoc(Path)
            SSDoc.ReadProperties(FMain.PropertiesData)
        Catch ex As Exception
            Proceed = False
        End Try

        If Proceed Then
            OldStatus = SSDoc.GetStatus()

            If (Not OldStatus = Nothing) AndAlso (Not OldStatus.ToLower = "Available".ToLower) Then
                Proceed = SSDoc.SetStatus("Available")
            End If
        End If

        If Not Proceed Then
            ErrorMessage = "Change status to 'Available' did not succeed"
        Else
            If SSDoc IsNot Nothing Then SSDoc.Save()
        End If

        If SSDoc IsNot Nothing Then SSDoc.Close()

        Return ErrorMessage
    End Function

    Private Function SetEndingStatus(
        OldStatus As String,
        Path As String) As String

        Dim ErrorMessage As String = ""
        Dim Proceed As Boolean = True

        Dim NewStatus As String = ""

        Dim SSDoc As HCStructuredStorageDoc = Nothing

        Try
            SSDoc = New HCStructuredStorageDoc(Path)
            SSDoc.ReadProperties(FMain.PropertiesData)
        Catch ex As Exception
            Proceed = False
        End Try

        If Proceed Then
            If FMain.ProcessAsAvailableRevert Then
                If Not OldStatus.ToLower = "Available".ToLower Then
                    Proceed = SSDoc.SetStatus(OldStatus)
                    If Not Proceed Then
                        ErrorMessage = String.Format("Change status to '{0}' did not succeed", OldStatus)
                    End If
                End If

            ElseIf FMain.ProcessAsAvailableChange Then
                NewStatus = GetNewStatus(OldStatus)
                If Not NewStatus.ToLower = "Available".ToLower Then
                    Proceed = SSDoc.SetStatus(NewStatus)
                    If Not Proceed Then
                        ErrorMessage = String.Format("Change status to '{0}' did not succeed", NewStatus)
                    End If
                End If
            End If

        End If

        If Proceed Then
            If SSDoc IsNot Nothing Then SSDoc.Save()
        End If

        If SSDoc IsNot Nothing Then SSDoc.Close()

        Return ErrorMessage

    End Function

    Private Function GetNewStatus(OldStatus As String) As String
        Dim NewStatus As String = Nothing

        If OldStatus = "Available" Then
            NewStatus = FMain.StatusAtoX
        End If
        If OldStatus = "Baselined" Then
            NewStatus = FMain.StatusBtoX
        End If
        If OldStatus = "InReview" Then
            NewStatus = FMain.StatusIRtoX
        End If
        If OldStatus = "InWork" Then
            NewStatus = FMain.StatusIWtoX
        End If
        If OldStatus = "Obsolete" Then
            NewStatus = FMain.StatusOtoX
        End If
        If OldStatus = "Released" Then
            NewStatus = FMain.StatusRtoX
        End If

        Return NewStatus
    End Function

End Class
