﻿Option Strict On

Public Class TopLevelAssemblyUtilities
    Private _mainInstance As Form1
    ' Private IndexedDrives As New List(Of String)

    Public Sub New(mainInstance As Form1)
        _mainInstance = mainInstance
        ' IndexedDrives = GetIndexedDrives()

        'Dim msg As String = ""
        'Dim IndexedDrive As String
        'For Each IndexedDrive In IndexedDrives
        '    msg = String.Format("{0}{1}{2}", msg, IndexedDrive, vbCrLf)
        'Next
        'MsgBox(msg)

    End Sub

    Public Function GetLinks(SearchType As String,
                             TopLevelFolder As String,
                             TopLevelAssembly As String,
                             ActiveFileExtensionsList As List(Of String),
                             Optional Report As Boolean = False) As List(Of String)
        Dim Foundfiles As New List(Of String)

        If SearchType.ToLower = "topdown" Then
            Foundfiles = GetLinksTopDown(TopLevelFolder, TopLevelAssembly, ActiveFileExtensionsList, Report)
        Else
            Foundfiles = GetLinksBottomUp(TopLevelFolder, TopLevelAssembly, ActiveFileExtensionsList)
        End If
        Return Foundfiles
    End Function

    Public Function GetLinksTopDown(TopLevelFolder As String,
                                    TopLevelAssembly As String,
                                    ActiveFileExtensionsList As List(Of String),
                                    Report As Boolean) As List(Of String)

        Dim DMApp As New DesignManager.Application
        Dim AllLinkedFilenames As New List(Of String)
        Dim FoundFiles As New List(Of String)
        Dim FileExtension As String
        Dim AllFilenames As New Dictionary(Of String, String)
        Dim LinkDict As New Dictionary(Of String, Dictionary(Of String, List(Of String)))
        Dim UnrelatedFiles As New List(Of String)

        ' DMApp = New DesignManager.Application()

        DMApp.Visible = 1  ' So it can be seen and closed in case of program malfunction.

        Form1.Activate()

        AllFilenames = GetAllFilenamesTopDown(TopLevelFolder)
        If Not AllFilenames.Keys.Contains(TopLevelAssembly.ToLower) Then
            AllFilenames.Add(TopLevelAssembly.ToLower, TopLevelAssembly)
        End If

        LinkDict = CreateLinkDict(AllFilenames, LinkDict)

        LinkDict = PopulateLinkDict(DMApp, LinkDict, TopLevelFolder)

        If CheckInterruptRequest() Then
            DMApp.Quit()
            Return FoundFiles
        End If
        LinkDict = VisitLinks(TopLevelAssembly, LinkDict, TopLevelFolder)

        For Each Filename In LinkDict.Keys
            FileExtension = System.IO.Path.GetExtension(Filename).Replace(".", "*.")
            If ActiveFileExtensionsList.Contains(FileExtension) Then
                If LinkDict(Filename)("Visited")(0) = "True" Then
                    FoundFiles.Add(Filename)
                Else
                    UnrelatedFiles.Add(Filename)
                End If
            End If
        Next

        If Report Then
            If UnrelatedFiles.Count > 0 Then
                Dim Timestamp As String = System.DateTime.Now.ToString("yyyyMMdd_HHmmss")
                Dim LogfileName As String
                Dim msg As String
                LogfileName = TopLevelFolder + "\Housekeeper_" + Timestamp + "_Unrelated_Files.log"

                Try
                    Using writer As New IO.StreamWriter(LogfileName, True)
                        For Each Filename In UnrelatedFiles
                            Filename = Filename.Replace(TopLevelFolder, "")
                            writer.WriteLine(String.Format(Filename))
                        Next
                    End Using

                    LogfileName = LogfileName.Replace(TopLevelFolder, "")
                    msg = String.Format("Files unrelated to top level assembly found.  See log file{0}", vbCrLf)
                    msg += LogfileName
                    MsgBox(msg)
                Catch ex As Exception
                End Try


            End If

        End If

        DMApp.Quit()

        Return FoundFiles

    End Function

    Private Function CreateLinkDict(
         AllFilenames As Dictionary(Of String, String),
         LinkDict As Dictionary(Of String, Dictionary(Of String, List(Of String)))
         ) As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim FilenameLowerCase As String
        Dim Filename As String

        For Each FilenameLowerCase In AllFilenames.Keys
            Filename = AllFilenames(FilenameLowerCase)

            LinkDict.Add(Filename, New Dictionary(Of String, List(Of String)))

            LinkDict(Filename).Add("Visited", New List(Of String))
            LinkDict(Filename)("Visited").Add("False")

            LinkDict(Filename).Add("Contains", New List(Of String))

            LinkDict(Filename).Add("ContainedBy", New List(Of String))
        Next

        Return LinkDict
    End Function

    Private Function PopulateLinkDict(
         DMApp As DesignManager.Application,
         LinkDict As Dictionary(Of String, Dictionary(Of String, List(Of String))),
         TopLevelFolder As String
         ) As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim Filename As String

        For Each Filename In LinkDict.Keys
            Form1.TextBoxStatus.Text = Filename

            If CheckInterruptRequest() Then
                Return LinkDict
            End If

            ' LinkDict = GetDownstreamLinks(DMApp, LinkDict, Filename)
            LinkDict = GetContains(DMApp, LinkDict, Filename, TopLevelFolder)
        Next

        'LinkDict = GetUpstreamLinks(LinkDict)
        LinkDict = GetContainedBy(LinkDict, TopLevelFolder)

        Return LinkDict
    End Function

    Private Function GetContains(
        DMApp As DesignManager.Application,
        LinkDict As Dictionary(Of String, Dictionary(Of String, List(Of String))),
        Filename As String,
        TopLevelFolder As String
         ) As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim DMDoc As DesignManager.Document
        Dim LinkedDocs As DesignManager.LinkedDocuments
        Dim LinkedDoc As DesignManager.Document
        Dim CorrectedFilename As String
        ' Dim tf As Boolean

        If CheckInterruptRequest() Then
            Return LinkDict
        End If

        UpdateStatus("Follow Links", Filename, TopLevelFolder)

        DMDoc = CType(DMApp.Open(Filename), DesignManager.Document)

        ' Some files are corrrupt or have other problems
        Try
            LinkedDocs = CType(DMDoc.LinkedDocuments, DesignManager.LinkedDocuments)

            For Each LinkedDoc In LinkedDocs
                CorrectedFilename = GetCorrectedFilename(LinkDict, LinkedDoc.FullName)
                If Not LinkDict(Filename)("Contains").Contains(CorrectedFilename) Then
                    If CorrectedFilename <> "" Then
                        LinkDict(Filename)("Contains").Add(CorrectedFilename)
                    End If
                End If
            Next
        Catch ex As Exception
        End Try

        Return LinkDict
    End Function

    Private Function GetContainedBy(
        LinkDict As Dictionary(Of String, Dictionary(Of String, List(Of String))),
        TopLevelFolder As String
         ) As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim Filename As String
        Dim ContainsFilename As String

        For Each Filename In LinkDict.Keys
            UpdateStatus("Contained By", Filename, TopLevelFolder)
            For Each ContainsFilename In LinkDict(Filename)("Contains")
                LinkDict(ContainsFilename)("ContainedBy").Add(Filename)
            Next
        Next

        Return LinkDict
    End Function

    Private Function VisitLinks(
        Filename As String,
        LinkDict As Dictionary(Of String, Dictionary(Of String, List(Of String))),
        TopLevelFolder As String
        ) As Dictionary(Of String, Dictionary(Of String, List(Of String)))

        Dim LinkedFilename As String
        Dim ContainedByFilename As String
        Dim Extension As String

        UpdateStatus("Visit Links", Filename, TopLevelFolder)

        'For Each LinkedFilename In LinkDict(Filename)("Contains")
        '    If Not LinkDict(LinkedFilename)("Visited")(0) = "True" Then
        '        LinkDict(LinkedFilename)("Visited")(0) = "True"
        '        LinkDict = VisitLinks(LinkedFilename, LinkDict, TopLevelFolder)
        '    End If
        'Next

        'For Each LinkedFilename In LinkDict(Filename)("ContainedBy")
        '    If Not LinkDict(LinkedFilename)("Visited")(0) = "True" Then
        '        LinkDict(LinkedFilename)("Visited")(0) = "True"
        '        LinkDict = VisitLinks(LinkedFilename, LinkDict, TopLevelFolder)
        '    End If
        'Next

        LinkDict(Filename)("Visited")(0) = "True"
        For Each ContainedByFilename In LinkDict(Filename)("ContainedBy")
            Extension = IO.Path.GetExtension(ContainedByFilename)
            If Extension = ".dft" Then
                LinkDict(ContainedByFilename)("Visited")(0) = "True"
            End If
        Next

        For Each LinkedFilename In LinkDict(Filename)("Contains")
            If Not LinkDict(LinkedFilename)("Visited")(0) = "True" Then
                LinkDict(LinkedFilename)("Visited")(0) = "True"
                For Each ContainedByFilename In LinkDict(LinkedFilename)("ContainedBy")
                    Extension = IO.Path.GetExtension(ContainedByFilename)
                    If Extension = ".dft" Then
                        LinkDict(ContainedByFilename)("Visited")(0) = "True"
                    End If
                Next
                LinkDict = VisitLinks(LinkedFilename, LinkDict, TopLevelFolder)
            End If
        Next

        Return LinkDict
    End Function

    Private Function GetCorrectedFilename(
        LinkDict As Dictionary(Of String, Dictionary(Of String, List(Of String))),
        Filename As String
        ) As String

        ' The CorrectFilename respects the capitalization as it is on disk
        ' which may not match the capitilization of the link

        Dim CorrectFilename As String = ""

        For Each CorrectFilename In LinkDict.Keys
            If Filename.Contains("!") Then
                If CorrectFilename.ToLower = Filename.ToLower.Split("!"c)(0) Then
                    Return CorrectFilename
                End If
            End If
            If CorrectFilename.ToLower = Filename.ToLower Then
                Return CorrectFilename
            End If
        Next

        Return ""
    End Function

    Private Function FollowLinksTopDown(LinkedDocument As DesignManager.Document,
                                 AllLinkedFilenames As List(Of String)) As List(Of String)
        Dim LinkedDocs As DesignManager.LinkedDocuments
        Dim LinkedDoc As DesignManager.Document
        Dim msg As String
        Dim ValidExtensions As New List(Of String)
        Dim Extension As String

        ValidExtensions.Add(".asm")
        ValidExtensions.Add(".par")
        ValidExtensions.Add(".psm")
        ValidExtensions.Add(".dft")

        If FileIO.FileSystem.FileExists(LinkedDocument.FullName) Then
            If Not AllLinkedFilenames.Contains(LinkedDocument.FullName) Then
                AllLinkedFilenames.Add(LinkedDocument.FullName)

                msg = LinkedDocument.FullName.Replace(Form1.TextBoxInputDirectory.Text, "")
                msg = "Follow Links " + msg
                Form1.TextBoxStatus.Text = msg

                ' In case of corrupted file or other problem
                Try
                    LinkedDocs = CType(LinkedDocument.LinkedDocuments, DesignManager.LinkedDocuments)
                    If LinkedDocs.Count > 0 Then
                        For Each LinkedDoc In LinkedDocs
                            Extension = IO.Path.GetExtension(LinkedDoc.FullName)
                            If ValidExtensions.Contains(Extension) Then
                                AllLinkedFilenames = FollowLinksTopDown(LinkedDoc, AllLinkedFilenames)
                            End If
                        Next
                    End If
                Catch ex As Exception
                End Try
            End If
        End If

        Return AllLinkedFilenames

    End Function

    Private Function GetAllFilenamesTopDown(TopLevelFolder As String) As Dictionary(Of String, String)
        Dim AllFilenames As IReadOnlyCollection(Of String)
        Dim Filename As String
        Dim ActiveFileExtensionsList As New List(Of String)
        Dim AllFilenamesDict As New Dictionary(Of String, String)

        Form1.TextBoxStatus.Text = "Getting all filenames."

        ActiveFileExtensionsList.Add("*.asm")
        ActiveFileExtensionsList.Add("*.par")
        ActiveFileExtensionsList.Add("*.psm")
        ActiveFileExtensionsList.Add("*.dft")

        AllFilenames = FileIO.FileSystem.GetFiles(TopLevelFolder,
                        FileIO.SearchOption.SearchAllSubDirectories,
                        ActiveFileExtensionsList.ToArray)

        For Each Filename In AllFilenames
            AllFilenamesDict.Add(Filename.ToLower, Filename)
        Next

        Form1.TextBoxStatus.Text = "Done getting all filenames."

        Return AllFilenamesDict

    End Function





    Public Function GetLinksBottomUp(TopLevelFolder As String,
                             TopLevelAssembly As String,
                             ActiveFileExtensionsList As List(Of String)) As List(Of String)

        Dim DMApp As New DesignManager.Application
        Dim AllLinkedFilenames As New List(Of String)
        Dim FoundFiles As New List(Of String)
        Dim FileExtension As String
        Dim AllFilenames As New Dictionary(Of String, String)
        Dim tf As Boolean
        Dim IndexedDrives As New List(Of String)
        Dim IsDriveIndexed As Boolean = False

        IndexedDrives = GetIndexedDrives()

        If IndexedDrives.Count > 0 Then
            For Each IndexedDrive In IndexedDrives
                If TopLevelFolder.ToLower().StartsWith(IndexedDrive.ToLower()) Then
                    IsDriveIndexed = True
                    Exit For
                End If
            Next
        End If

        DMApp.Visible = 1
        DMApp.DisplayAlerts = 0

        Form1.Activate()

        AllLinkedFilenames = FollowLinksBottomUp(DMApp, TopLevelAssembly, AllLinkedFilenames,
                                                 TopLevelFolder, AllFilenames, IsDriveIndexed)

        DMApp.Quit()

        For Each Filename In AllLinkedFilenames
            FileExtension = System.IO.Path.GetExtension(Filename).Replace(".", "*.")
            tf = ActiveFileExtensionsList.Contains(FileExtension)
            tf = tf And (Not FoundFiles.Contains(Filename, StringComparer.OrdinalIgnoreCase))
            If tf Then
                FoundFiles.Add(Filename)
            End If
        Next

        Return FoundFiles

    End Function

    Private Function FollowLinksBottomUp(DMApp As DesignManager.Application,
                                         Filename As String,
                                         AllLinkedFilenames As List(Of String),
                                         TopLevelFolder As String,
                                         AllFilenames As Dictionary(Of String, String),
                                         IsDriveIndexed As Boolean) As List(Of String)

        Dim DMDoc As DesignManager.Document
        Dim LinkedDocs As DesignManager.LinkedDocuments
        Dim LinkedDoc As DesignManager.Document
        Dim LinkedDocName As String
        Dim ValidExtensions As New List(Of String)
        Dim Extension As String
        Dim WhereUsedFiles As New List(Of String)
        Dim WhereUsedFile As String
        Dim tf As Boolean

        If CheckInterruptRequest() Then
            Return AllLinkedFilenames
        End If

        ValidExtensions.Add(".asm")
        ValidExtensions.Add(".par")
        ValidExtensions.Add(".psm")
        ValidExtensions.Add(".dft")

        If FileIO.FileSystem.FileExists(Filename) Then
            tf = Not AllLinkedFilenames.Contains(Filename, StringComparer.OrdinalIgnoreCase)
            If tf Then
                AllLinkedFilenames.Add(Filename)

                UpdateStatus("Follow Links", Filename, TopLevelFolder)

                ' In case of corrupted file or other problem
                Try
                    DMDoc = CType(DMApp.OpenFileInDesignManager(Filename), DesignManager.Document)

                    ' Get any draft files containing this file.
                    WhereUsedFiles = GetWhereUsedBottomUp(DMApp, TopLevelFolder, DMDoc.FullName, IsDriveIndexed)
                    For Each WhereUsedFile In WhereUsedFiles
                        Extension = IO.Path.GetExtension(WhereUsedFile)
                        If Extension = ".dft" Then
                            If Not AllLinkedFilenames.Contains(WhereUsedFile, StringComparer.OrdinalIgnoreCase) Then
                                AllLinkedFilenames.Add(WhereUsedFile)
                            End If
                        End If
                    Next

                    ' Follow links contained by this file, if any.
                    LinkedDocs = CType(DMDoc.LinkedDocuments, DesignManager.LinkedDocuments)
                    If LinkedDocs.Count > 0 Then
                        For Each LinkedDoc In LinkedDocs

                            ' Get FOP status
                            Dim FOPStatus As Integer
                            LinkedDoc.IsDocumentFOP(FOPStatus)

                            If Not (FOPStatus = DesignManager.DocFOPStatus.FOPMasterDocument) Then
                                LinkedDocName = LinkedDoc.FullName
                                If LinkedDocName.Contains("!") Then
                                    LinkedDocName = LinkedDocName.Split("!"c)(0)
                                End If
                                Extension = IO.Path.GetExtension(LinkedDocName)
                                If ValidExtensions.Contains(Extension) Then
                                    AllLinkedFilenames = FollowLinksBottomUp(DMApp, LinkedDocName, AllLinkedFilenames,
                                                                             TopLevelFolder, AllFilenames, IsDriveIndexed)
                                End If
                            End If
                        Next

                    End If

                    DMDoc.Close()
                    System.Threading.Thread.Sleep(100)
                Catch ex As Exception
                    ' MsgBox(Filename)
                End Try
            End If
        End If


            Return AllLinkedFilenames

    End Function



    Private Function GetWhereUsedBottomUp(
                     DMApp As DesignManager.Application,
                     TopLevelFolder As String,
                     Filename As String,
                     IsDriveIndexed As Boolean) As List(Of String)

        Dim AllWhereUsedFileNames As New List(Of String)
        ' Dim msg As String
        Dim Extension As String

        Dim WhereUsedDocuments As New List(Of DesignManager.Document)
        Dim WhereUsedDocument As DesignManager.Document

        Dim arrDocUsed As Object = Nothing

        If CheckInterruptRequest() Then
            Return AllWhereUsedFileNames
        End If
        UpdateStatus("Where Used", Filename, TopLevelFolder)

        Extension = IO.Path.GetExtension(Filename)

        If Not Extension = ".dft" Then  ' Draft files are not "Used" anywhere.
            If Not IsDriveIndexed Then
                'This "resets" DMApp.FindWhereUsed().  Somehow.
                DMApp.WhereUsedCriteria(Nothing, True) = TopLevelFolder

                'Finds the first WhereUsed Document, if any.
                WhereUsedDocument = CType(
                DMApp.FindWhereUsed(FileIO.FileSystem.GetFileInfo(Filename)),
                DesignManager.Document)

                While Not WhereUsedDocument Is Nothing
                    If Not AllWhereUsedFileNames.Contains(WhereUsedDocument.FullName) Then
                        ' For bottom_up search, the only applicable where used results are draft files
                        If IO.Path.GetExtension(WhereUsedDocument.FullName) = ".dft" Then
                            AllWhereUsedFileNames.Add(WhereUsedDocument.FullName)
                        End If
                    End If
                    'Finds the next WhereUsed document, if any.
                    WhereUsedDocument = CType(DMApp.FindWhereUsed(), DesignManager.Document)
                End While

            Else  'It is indexed
                Try
                    DMApp.WhereUsedCriteria(Nothing, True) = TopLevelFolder

                    DMApp.FindWhereUsedDocuments(FileIO.FileSystem.GetFileInfo(Filename), arrDocUsed)

                    For Each item As String In DirectCast(arrDocUsed, Array)
                        If Not AllWhereUsedFileNames.Contains(item) Then
                            ' For bottom_up search, the only applicable where used results are draft files
                            If IO.Path.GetExtension(item) = ".dft" Then
                                AllWhereUsedFileNames.Add(item)
                            End If
                        End If
                    Next

                Catch ex As Exception
                    MsgBox(ex.ToString)
                End Try

            End If

        End If

        Return AllWhereUsedFileNames

    End Function

    Private Sub UpdateStatus(Description As String, Filename As String, TopLevelFolder As String)
        Dim msg As String

        msg = Filename.Replace(TopLevelFolder, "")
        msg = String.Format("{0} {1}", Description, msg)

        Form1.TextBoxStatus.Text = msg
    End Sub

    Private Function CheckInterruptRequest() As Boolean
        Dim tf As Boolean = False

        System.Windows.Forms.Application.DoEvents()
        If Form1.StopProcess Then
            Form1.TextBoxStatus.Text = "Processing aborted"
            tf = True
        End If

        Return tf
    End Function

    Private Function GetIndexedDrives() As List(Of String)

        Dim SearchScopeFilename As String = _mainInstance.TextBoxFastSearchScopeFilename.Text

        Dim SearchScope As String() = Nothing
        Dim CommentString As String = "\\ "
        Dim Line As String
        Dim IndexedDrives As New List(Of String)

        Try
            SearchScope = IO.File.ReadAllLines(SearchScopeFilename)
        Catch ex As Exception
            MsgBox(String.Format("Error reading {0}", SearchScopeFilename))
            'Exit Function
        End Try

        If (SearchScope.Count > 0) And (SearchScope IsNot Nothing) Then
            For Each item As String In SearchScope
                Line = item.TrimStart()
                If (Not Line.StartsWith(CommentString)) And (Line.Count > 0) Then
                    IndexedDrives.Add(Line)
                End If
            Next
        End If

        Return IndexedDrives
    End Function



End Class