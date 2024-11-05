﻿Option Strict On
Imports Newtonsoft.Json

Public Class TaskEditVariables
    Inherits Task

    Private _JSONString As String
    Public Property JSONString As String
        Get
            Return _JSONString
        End Get
        Set(value As String)
            _JSONString = value
            If Me.TaskOptionsTLP IsNot Nothing Then
                CType(ControlsDict(ControlNames.JSONString.ToString), TextBox).Text = value
            End If
        End Set
    End Property

    Private _AutoAddMissingVariable As Boolean
    Public Property AutoAddMissingVariable As Boolean
        Get
            Return _AutoAddMissingVariable
        End Get
        Set(value As Boolean)
            _AutoAddMissingVariable = value
            If Me.TaskOptionsTLP IsNot Nothing Then
                CType(ControlsDict(ControlNames.AutoAddMissingVariable.ToString), CheckBox).Checked = value
            End If
        End Set
    End Property


    Private _AutoHideOptions As Boolean
    Public Property AutoHideOptions As Boolean
        Get
            Return _AutoHideOptions
        End Get
        Set(value As Boolean)
            _AutoHideOptions = value
            If Me.TaskOptionsTLP IsNot Nothing Then
                CType(ControlsDict(ControlNames.AutoHideOptions.ToString), CheckBox).Checked = value
            End If
        End Set
    End Property

    'Public Property TemplatePropertyDict As Dictionary(Of String, Dictionary(Of String, String))

    Public Property PropertiesData As PropertiesData

    Enum ControlNames
        Edit
        JSONString
        AutoAddMissingVariable
        AutoHideOptions
    End Enum

    Public Sub New()
        Me.Name = Me.ToString.Replace("Housekeeper.", "")
        Me.Description = GenerateLabelText()
        Me.HelpText = GetHelpText()
        Me.RequiresSave = True
        Me.AppliesToAssembly = True
        Me.AppliesToPart = True
        Me.AppliesToSheetmetal = True
        Me.AppliesToDraft = False
        Me.HasOptions = True
        Me.HelpURL = GenerateHelpURL(Description)
        Me.Image = My.Resources.TaskEditVariables
        Me.Category = "Edit"
        'Me.RequiresTemplatePropertyDict = True
        Me.RequiresPropertiesData = True
        SetColorFromCategory(Me)

        GenerateTaskControl()
        TaskOptionsTLP = GenerateTaskOptionsTLP()
        Me.TaskControl.AddTaskOptionsTLP(TaskOptionsTLP)

        ' Options
        Me.JSONString = ""
        Me.AutoAddMissingVariable = False

        'Me.TemplatePropertyDict = New Dictionary(Of String, Dictionary(Of String, String))
        Me.PropertiesData = New PropertiesData

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
        'Dim SupplementalErrorMessage As New Dictionary(Of Integer, List(Of String))
        'Dim SupplementalExitStatus As Integer = 0

        Dim tmpVariablesToEditDict As New Dictionary(Of String, Dictionary(Of String, String))
        Dim VariablesToEditDict As New Dictionary(Of Integer, Dictionary(Of String, String))
        Dim ColumnIndexString As String
        Dim ColumnIndex As Integer
        Dim RowIndex As Integer

        'Dim VariableName As String

        Dim DocDimensionDict As New Dictionary(Of String, SolidEdgeFrameworkSupport.Dimension)
        Dim DocVariableDict As New Dictionary(Of String, SolidEdgeFramework.variable)

        Dim Variables As SolidEdgeFramework.Variables = Nothing
        Dim VariableListObject As SolidEdgeFramework.VariableList = Nothing
        Dim Variable As SolidEdgeFramework.variable = Nothing
        Dim Dimension As SolidEdgeFrameworkSupport.Dimension = Nothing
        'Dim VariableTypeName As String

        Dim VariablesToEdit As String = ""
        Dim VariablesToExposeDict As New Dictionary(Of String, String)

        Dim DocType As String

        Dim UC As New UtilsCommon

        'Dim Proceed As Boolean = True
        Dim tf As Boolean
        'Dim s As String

        DocType = UC.GetDocType(SEDoc)

        VariablesToEdit = Me.JSONString

        If Not VariablesToEdit = "" Then
            tmpVariablesToEditDict = JsonConvert.DeserializeObject(Of Dictionary(Of String, Dictionary(Of String, String)))(VariablesToEdit)
            ' Dictionary format
            '{
            '    "1":
            '    {
            '        "VariableName":"v1"
            '        "Formula":"1",
            '        "UnitType":"Distance",
            '        "Expose":"True",
            '        "ExposeName":"var1"
            '    },
            '    "2":
            '    ...
            '}

            For Each ColumnIndexString In tmpVariablesToEditDict.Keys
                ColumnIndex = CInt(ColumnIndexString)
                VariablesToEditDict(ColumnIndex) = New Dictionary(Of String, String)

                For Each Key In tmpVariablesToEditDict(ColumnIndexString).Keys
                    VariablesToEditDict(ColumnIndex)(Key) = tmpVariablesToEditDict(ColumnIndexString)(Key)
                Next

            Next

        Else
            ExitStatus = 1
            ErrorMessageList.Add("No variables provided")
        End If

        If ExitStatus = 0 Then
            Variables = DirectCast(SEDoc.Variables, SolidEdgeFramework.Variables)

            DocDimensionDict = UC.GetDocDimensions(SEDoc)
            If DocDimensionDict Is Nothing Then
                ExitStatus = 1
                ErrorMessageList.Add("Unable to access dimensions")
            End If

            DocVariableDict = UC.GetDocVariables(SEDoc)
            If DocVariableDict Is Nothing Then
                ExitStatus = 1
                ErrorMessageList.Add("Unable to access variables")
            End If

        End If

        If ExitStatus = 0 Then

            ' Process variables.

            For Each RowIndex In VariablesToEditDict.Keys
                Dim VariableName As String = VariablesToEditDict(RowIndex)("VariableName").Trim

                Dim Formula As String = VariablesToEditDict(RowIndex)("Formula").Trim
                Dim FullName As String = UC.SplitFOAName(SEDoc.FullName)("Filename")

                Dim tmpFormula As String = Formula
                Try
                    'Formula = UC.SubstitutePropertyFormula(SEDoc, Nothing, Nothing, FullName, Formula, ValidFilenameRequired:=False, Me.TemplatePropertyDict)
                    Formula = UC.SubstitutePropertyFormula(
                    SEDoc, Nothing, Nothing, FullName, Formula, ValidFilenameRequired:=False, Me.PropertiesData)
                Catch ex As Exception
                    ExitStatus = 1
                    ErrorMessageList.Add(String.Format("Could not process formula '{0}'", tmpFormula))
                    Continue For
                End Try

                Dim UnitType As SolidEdgeConstants.UnitTypeConstants = UC.GetUnitType(VariablesToEditDict(RowIndex)("UnitType").Trim)
                Dim Expose As Boolean = VariablesToEditDict(RowIndex)("Expose").Trim.ToLower = "true"
                Dim ExposeName As String = VariablesToEditDict(RowIndex)("ExposeName").Trim

                tf = DocDimensionDict.Keys.Contains(VariableName)
                tf = tf Or DocVariableDict.Keys.Contains(VariableName)

                If Not tf Then  ' Add it.
                    If Me.AutoAddMissingVariable Then
                        If Formula = "" Then  ' Can't add a variable without a formula
                            ExitStatus = 1
                            ErrorMessageList.Add(String.Format("Unable to add variable named '{0}'.  No value or formula supplied.", VariableName))
                            Continue For
                        End If

                        Try
                            ' Pretty sure this must be a variable, not a dimension.
                            Variable = CType(Variables.Add(VariableName, Formula, UnitType), SolidEdgeFramework.variable)
                            If Expose Then
                                Variable.Expose = CInt(Expose)
                                If Not ExposeName = "" Then
                                    Variable.ExposeName = ExposeName
                                End If
                            End If
                        Catch ex As Exception
                            ExitStatus = 1
                            ErrorMessageList.Add(String.Format("Unable to add and/or expose variable '{0}'", VariableName))

                        End Try

                    End If

                Else  ' Edit and/or Expose.
                    Try
                        If DocDimensionDict.Keys.Contains(VariableName) Then
                            Dimension = DocDimensionDict(VariableName)
                            If Expose Then
                                Dimension.Expose = CInt(Expose)
                                If Not ExposeName = "" Then
                                    Dimension.ExposeName = ExposeName
                                End If
                            End If
                            If Not Formula = "" Then
                                Dimension.Formula = Formula
                            End If

                        ElseIf DocVariableDict.Keys.Contains(VariableName) Then
                            Variable = DocVariableDict(VariableName)
                            If Expose Then
                                Variable.Expose = CInt(Expose)
                                If Not ExposeName = "" Then
                                    Variable.ExposeName = ExposeName
                                End If
                            End If
                            If Not Formula = "" Then
                                Variable.Formula = Formula
                            End If

                        End If
                    Catch ex As Exception
                        ExitStatus = 1
                        ErrorMessageList.Add(String.Format("Unable to change variable '{0}'", VariableName))
                    End Try

                End If
            Next

        End If

        If ExitStatus = 0 Then
            If SEDoc.ReadOnly Then
                ExitStatus = 1
                ErrorMessageList.Add("Cannot save document marked 'Read Only'")
            Else
                SEDoc.Save()
                SEApp.DoIdle()
            End If
        End If

        If SEDoc.ReadOnly Then
            ExitStatus = 1
            ErrorMessageList.Add("Cannot save document marked 'Read Only'")
        Else
            SEDoc.Save()
            SEApp.DoIdle()
        End If

        ErrorMessage(ExitStatus) = ErrorMessageList
        Return ErrorMessage
    End Function


    Private Function GenerateTaskOptionsTLP() As ExTableLayoutPanel
        Dim tmpTLPOptions = New ExTableLayoutPanel

        Dim RowIndex As Integer
        Dim CheckBox As CheckBox
        Dim Button As Button
        Dim TextBox As TextBox

        'Dim IU As New InterfaceUtilities

        FormatTLPOptions(tmpTLPOptions, "TLPOptions", 3)

        RowIndex = 0

        Button = FormatOptionsButton(ControlNames.Edit.ToString, "Edit")
        AddHandler Button.Click, AddressOf ButtonOptions_Click
        tmpTLPOptions.Controls.Add(Button, 0, RowIndex)
        ControlsDict(Button.Name) = Button

        TextBox = FormatOptionsTextBox(ControlNames.JSONString.ToString, "")
        TextBox.BackColor = Color.FromArgb(255, 240, 240, 240)
        AddHandler TextBox.TextChanged, AddressOf TextBoxOptions_Text_Changed
        tmpTLPOptions.Controls.Add(TextBox, 1, RowIndex)
        ControlsDict(TextBox.Name) = TextBox

        RowIndex += 1

        CheckBox = FormatOptionsCheckBox(ControlNames.AutoAddMissingVariable.ToString, "Add any variable not already in the file")
        AddHandler CheckBox.CheckedChanged, AddressOf CheckBoxOptions_Check_Changed
        tmpTLPOptions.Controls.Add(CheckBox, 0, RowIndex)
        tmpTLPOptions.SetColumnSpan(CheckBox, 2)
        ControlsDict(CheckBox.Name) = CheckBox

        RowIndex += 1

        CheckBox = FormatOptionsCheckBox(ControlNames.AutoHideOptions.ToString, ManualOptionsOnlyString)
        'CheckBox.Checked = True
        AddHandler CheckBox.CheckedChanged, AddressOf CheckBoxOptions_Check_Changed
        ControlsDict(CheckBox.Name) = CheckBox

        tmpTLPOptions.Controls.Add(CheckBox, 0, RowIndex)
        tmpTLPOptions.SetColumnSpan(CheckBox, 2)

        Return tmpTLPOptions
    End Function

    Private Sub InitializeOptionProperties()
        Dim CheckBox As CheckBox
        Dim TextBox As TextBox

        TextBox = CType(ControlsDict(ControlNames.JSONString.ToString), TextBox)
        Me.JSONString = TextBox.Text

        CheckBox = CType(ControlsDict(ControlNames.AutoAddMissingVariable.ToString), CheckBox)
        Me.AutoAddMissingVariable = CheckBox.Checked

        CheckBox = CType(ControlsDict(ControlNames.AutoHideOptions.ToString), CheckBox)
        Me.AutoHideOptions = CheckBox.Checked

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

            If (Me.JSONString = "") Or (Me.JSONString = "{}") Then
                If Not ErrorMessageList.Contains(Me.Description) Then
                    ErrorMessageList.Add(Me.Description)
                End If
                ExitStatus = 1
                ErrorMessageList.Add(String.Format("{0}Select at least one variable to edit", Indent))
            End If

        End If

        If ExitStatus > 0 Then  ' Start conditions not met.
            ErrorMessage(ExitStatus) = ErrorMessageList
            Return ErrorMessage
        Else
            Return PriorErrorMessage
        End If

    End Function


    Public Sub ButtonOptions_Click(sender As System.Object, e As System.EventArgs)
        Dim Button = CType(sender, Button)
        Dim Name = Button.Name
        Dim TextBox As TextBox

        Select Case Name
            Case ControlNames.Edit.ToString
                Dim VariableInputEditor As New FormVariableInputEditor

                VariableInputEditor.JSONString = Me.JSONString
                VariableInputEditor.HelpURL = Me.HelpURL

                '' Workaround
                'Dim FileType = "asm"

                VariableInputEditor.ShowDialog()

                If VariableInputEditor.DialogResult = DialogResult.OK Then
                    Me.JSONString = VariableInputEditor.JSONString

                    TextBox = CType(ControlsDict(ControlNames.JSONString.ToString), TextBox)
                    TextBox.Text = Me.JSONString
                End If

            Case Else
                MsgBox(String.Format("{0} Name '{1}' not recognized", Me.Name, Name))
        End Select

    End Sub

    Public Sub CheckBoxOptions_Check_Changed(sender As System.Object, e As System.EventArgs)
        Dim Checkbox = CType(sender, CheckBox)
        Dim Name = Checkbox.Name
        'Dim Ctrl As Control
        'Dim Button As Button

        Select Case Name
            Case ControlNames.AutoAddMissingVariable.ToString
                Me.AutoAddMissingVariable = Checkbox.Checked

            Case ControlNames.AutoHideOptions.ToString
                Me.TaskControl.AutoHideOptions = Checkbox.Checked
                If Not Me.AutoHideOptions = TaskControl.AutoHideOptions Then
                    Me.AutoHideOptions = Checkbox.Checked
                End If

            Case Else
                MsgBox(String.Format("{0} Name '{1}' not recognized", Me.Name, Name))
        End Select

    End Sub

    Public Sub TextBoxOptions_Text_Changed(sender As System.Object, e As System.EventArgs)
        Dim TextBox = CType(sender, TextBox)
        Dim Name = TextBox.Name

        Select Case Name
            Case ControlNames.JSONString.ToString
                Me.JSONString = TextBox.Text
            Case Else
                MsgBox(String.Format("{0} Name '{1}' not recognized", Me.Name, Name))
        End Select

    End Sub


    Private Function GetHelpText() As String
        Dim HelpString As String

        HelpString = "Adds, changes, and/or exposes variables. "

        HelpString += vbCrLf + vbCrLf + "![EditVariables](My%20Project/media/task_edit_variables.png)"

        HelpString += vbCrLf + vbCrLf + "The information is entered on the Input Editor. Access the form using the `Edit` button. "

        HelpString += vbCrLf + vbCrLf + "![Variable_Editor](My%20Project/media/variable_input_editor.png)"

        HelpString += vbCrLf + vbCrLf + "The Variable name is required.  There are restrictions on the name.  "
        HelpString += "It cannot start with a number.  It can only contain letters and numbers and the "
        HelpString += "underscore `_` character."

        HelpString += vbCrLf + vbCrLf + "If a variable on the list is not in the file, it can optionally be added.  "
        HelpString += "Set the behavior on the Options panel. "

        HelpString += vbCrLf + vbCrLf + "The number/formula is not required if only exposing an existing variable, "
        HelpString += "otherwise it is.  If a formula references a variable not in the file, the "
        HelpString += "program will report an error.  You can use a file property in the formula.  "
        HelpString += "Right-click the formula text box and select Insert Property to do so.  "

        HelpString += vbCrLf + vbCrLf + "If exposing a variable, the Expose name defaults to the variable name. "
        HelpString += "You can optionally change it.  The Expose name does not have restrictions like the variable name. "

        HelpString += vbCrLf + vbCrLf + "The variables are processed in the order in the table. "
        HelpString += "To change the order, select a row and, on the toolbar `Row Tools` group, click the `Up` or `Down` arrow. "
        HelpString += "The `Delete` button removes the selected row. "

        HelpString += vbCrLf + vbCrLf + "You can save a setup for future use. "
        HelpString += "In the toolbar `Saved Settings` group, enter the name and click `Save`. "
        HelpString += "To retrieve a setting, click the down arrow and select it. "
        HelpString += "To remove a setting, select it and click `Delete`. "

        Return HelpString
    End Function

End Class
