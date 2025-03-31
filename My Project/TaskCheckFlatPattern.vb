﻿Option Strict On

Public Class TaskCheckFlatPattern

    Inherits Task

    Public Sub New()
        Me.Name = Me.ToString.Replace("Housekeeper.", "")
        Me.Description = GenerateLabelText()
        Me.HelpText = GetHelpText()
        Me.RequiresSave = False
        Me.AppliesToAssembly = False
        Me.AppliesToPart = True
        Me.AppliesToSheetmetal = True
        Me.AppliesToDraft = False
        Me.HasOptions = False
        Me.HelpURL = GenerateHelpURL(Description)
        Me.Image = My.Resources.TaskCheckFlatPattern
        Me.Category = "Check"
        SetColorFromCategory(Me)

        GenerateTaskControl()

        ' Options

    End Sub

    Public Overrides Function Process(
        ByVal SEDoc As SolidEdgeFramework.SolidEdgeDocument,
        ByVal Configuration As Dictionary(Of String, String),
        ByVal SEApp As SolidEdgeFramework.Application
        ) As Dictionary(Of Integer, List(Of String))

        Dim ErrorMessage As New Dictionary(Of Integer, List(Of String))

        ErrorMessage = InvokeSTAThread(
                               Of SolidEdgeFramework.SolidEdgeDocument,
                               Dictionary(Of String, String),
                               SolidEdgeFramework.Application,
                               Dictionary(Of Integer, List(Of String)))(
                                   AddressOf ProcessInternal,
                                   SEDoc,
                                   Configuration,
                                   SEApp)

        Return ErrorMessage

    End Function

    Public Overrides Function Process(ByVal FileName As String) As Dictionary(Of Integer, List(Of String))

        Dim ErrorMessage As New Dictionary(Of Integer, List(Of String))

        Return ErrorMessage

    End Function

    Private Function ProcessInternal(
        ByVal SEDoc As SolidEdgeFramework.SolidEdgeDocument,
        ByVal Configuration As Dictionary(Of String, String),
        ByVal SEApp As SolidEdgeFramework.Application
        ) As Dictionary(Of Integer, List(Of String))

        Dim ErrorMessageList As New List(Of String)
        Dim ExitStatus As Integer = 0
        Dim ErrorMessage As New Dictionary(Of Integer, List(Of String))

        Me.TaskLogger = Me.FileLogger.AddLogger(Me.Description)

        Dim UC As New UtilsCommon
        Dim DocType = UC.GetDocType(SEDoc)

        Dim FlatpatternModels As SolidEdgePart.FlatPatternModels = Nothing
        Dim FlatpatternModel As SolidEdgePart.FlatPatternModel

        Select Case DocType
            Case = "par"
                Dim tmpSEDoc = CType(SEDoc, SolidEdgePart.PartDocument)
                FlatpatternModels = tmpSEDoc.FlatPatternModels

            Case = "psm"
                Dim tmpSEDoc = CType(SEDoc, SolidEdgePart.SheetMetalDocument)
                FlatpatternModels = tmpSEDoc.FlatPatternModels

            Case Else
                MsgBox(String.Format("{0} DocType '{0}' not recognized", Me.Name, DocType))
        End Select

        '' Active flat environment to regenerate flat model then save part if no errors
        'If ExitStatus = 0 And FlatpatternModels.Count > 0 Then
        '    SEDoc.Activate()
        '    SEApp.DoIdle()
        '    SEApp.StartCommand(CType(SolidEdgeConstants.SheetMetalCommandConstants.SheetMetalToolsSelectTool, SolidEdgeFramework.SolidEdgeCommandConstants))
        '    SEApp.DoIdle()
        '    SEApp.StartCommand(CType(SolidEdgeConstants.SheetMetalCommandConstants.SheetMetalModelFlatPattern, SolidEdgeFramework.SolidEdgeCommandConstants))
        '    SEApp.DoIdle()
        '    'SEDoc.Save()
        '    SEApp.DoIdle()
        'End If


        If Not FlatpatternModels Is Nothing Then
            If FlatpatternModels.Count > 0 Then
                For Each FlatpatternModel In FlatpatternModels
                    If Not FlatpatternModel.IsUpToDate Then
                        ExitStatus = 1
                        ErrorMessageList.Add("Flat pattern is out of date")

                        TaskLogger.AddMessage("Flat pattern is out of date")

                    End If
                    If Not FlatpatternModel.FlatPatterns.Item(1).Status = SolidEdgePart.FeatureStatusConstants.igFeatureOK Then
                        ExitStatus = 1
                        ErrorMessageList.Add("Flat pattern is out of date")

                        TaskLogger.AddMessage("Flat pattern is out of date")

                    End If
                Next
            Else
                ExitStatus = 1
                ErrorMessageList.Add("No flat patterns found")

                TaskLogger.AddMessage("No flat patterns found")

            End If
        End If
        ErrorMessage(ExitStatus) = ErrorMessageList
        Return ErrorMessage

    End Function


    Public Overrides Function CheckStartConditions(
        PriorErrorMessage As Dictionary(Of Integer, List(Of String))
        ) As Dictionary(Of Integer, List(Of String))

        Dim PriorExitStatus As Integer = PriorErrorMessage.Keys(0)

        Dim ErrorMessage As New Dictionary(Of Integer, List(Of String))
        Dim ExitStatus As Integer = 0
        Dim ErrorMessageList = PriorErrorMessage(PriorExitStatus)
        Dim Indent = "    "

        If Me.IsSelectedTask Then
            ' Check start conditions.
            If Not (Me.IsSelectedAssembly Or Me.IsSelectedPart Or Me.IsSelectedSheetmetal Or Me.IsSelectedDraft) Then
                If Not ErrorMessageList.Contains(Me.Description) Then
                    ErrorMessageList.Add(Me.Description)
                End If
                ExitStatus = 1
                ErrorMessageList.Add(String.Format("{0}Select at least one type of file to process", Indent))
            End If

        End If

        If ExitStatus > 0 Then  ' Start conditions not met.
            ErrorMessage(ExitStatus) = ErrorMessageList
            Return ErrorMessage
        Else
            Return PriorErrorMessage
        End If

    End Function


    Private Function GetHelpText() As String
        Dim HelpString As String
        HelpString = "Checks for the existence of a flat pattern. "
        HelpString += "If one is found, checks if it is up to date. "

        Return HelpString
    End Function


End Class
