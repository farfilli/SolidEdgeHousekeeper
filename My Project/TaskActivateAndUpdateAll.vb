﻿Option Strict On
Imports System.Management.Automation

Public Class TaskActivateAndUpdateAll
    Inherits Task

    Public Sub New()
        Me.Name = Me.ToString.Replace("Housekeeper.", "")
        Me.Description = GenerateLabelText()
        Me.HelpText = GenerateHelpText()
        Me.RequiresSave = True
        Me.AppliesToAssembly = True
        Me.AppliesToPart = False
        Me.AppliesToSheetmetal = False
        Me.AppliesToDraft = False
        Me.HasOptions = False
        Me.HelpURL = GenerateHelpURL(Description)
        Me.Image = My.Resources.TaskActivateAndUpdateAll
        Me.Category = "Update"
        SetColorFromCategory(Me)

        GenerateTaskControl()

    End Sub

    Public Overrides Sub Process(
        ByVal SEDoc As SolidEdgeFramework.SolidEdgeDocument,
        ByVal SEApp As SolidEdgeFramework.Application)

        Me.TaskLogger = Me.FileLogger.AddLogger(Me.Description)

        InvokeSTAThread(
            Of SolidEdgeFramework.SolidEdgeDocument,
            SolidEdgeFramework.Application)(
                AddressOf ProcessInternal,
                SEDoc,
                SEApp)
    End Sub

    Public Overrides Sub Process(ByVal FileName As String)
        Me.TaskLogger = Me.FileLogger.AddLogger(Me.Description)
    End Sub

    Private Sub ProcessInternal(
        ByVal SEDoc As SolidEdgeFramework.SolidEdgeDocument,
        ByVal SEApp As SolidEdgeFramework.Application
        )

        Dim UC As New UtilsCommon


        If UC.GetDocType(SEDoc) = "asm" Then
            Dim tmpSEDoc As SolidEdgeAssembly.AssemblyDocument
            tmpSEDoc = CType(SEDoc, SolidEdgeAssembly.AssemblyDocument)

            tmpSEDoc.ActivateAll()
            tmpSEDoc.UpdateAll()
        End If

        If SEDoc.ReadOnly Then
            'ExitStatus = 1
            'ErrorMessageList.Add("Cannot save document marked 'Read Only'")

            TaskLogger.AddMessage("Cannot save document marked 'Read Only'")
        Else
            SEDoc.Save()
            SEApp.DoIdle()
        End If


        'ErrorMessage(ExitStatus) = ErrorMessageList
        'Return ErrorMessage
    End Sub


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


    Private Function GenerateHelpText() As String
        Dim HelpString As String
        HelpString = "Loads all assembly occurrences' geometry into memory and does an update. "
        HelpString += "Used mainly to eliminate the gray corners on assembly drawings. "
        HelpString += vbCrLf + vbCrLf + "Can run out of memory for very large assemblies."

        Return HelpString
    End Function



End Class
