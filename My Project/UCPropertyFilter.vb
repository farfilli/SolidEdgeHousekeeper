﻿Option Strict On

Public Class UCPropertyFilter
    Public Property PropertyFilter As FormPropertyFilter

    Private _Selected As Boolean
    Public Property Selected As Boolean
        Get
            Return _Selected
        End Get
        Set(value As Boolean)
            _Selected = value
            If Me.ExTableLayoutPanel1 IsNot Nothing Then
                CheckBoxSelected.Checked = value
            End If
        End Set
    End Property

    Private _Variable As String
    Public Property Variable As String
        Get
            Return _Variable
        End Get
        Set(Variable As String)
            _Variable = Variable
            If Me.ExTableLayoutPanel1 IsNot Nothing Then
                LabelVariable.Text = Variable
            End If
        End Set
    End Property


    Private _PropertySet As String
    Public Property PropertySet As String
        Get
            Return _PropertySet
        End Get
        Set(value As String)
            _PropertySet = value
            If Me.ExTableLayoutPanel1 IsNot Nothing Then
                ComboBoxPropertySet.Text = value
            End If
        End Set
    End Property

    Private _PropertyName As String
    Public Property PropertyName As String
        Get
            Return _PropertyName
        End Get
        Set(value As String)
            _PropertyName = value
            If Me.ExTableLayoutPanel1 IsNot Nothing Then
                If ComboBoxPropertyName.Items.Contains(value) Then
                    ComboBoxPropertyName.Text = value
                Else
                    If Form_Main.PropertiesData.GetAvailableList.Contains(value) Then
                        ComboBoxPropertyName.Items.Add(value)
                        ComboBoxPropertyName.Text = value
                    End If
                End If
            End If
        End Set
    End Property

    Private _Comparison As String
    Public Property Comparison As String
        Get
            Return _Comparison
        End Get
        Set(value As String)
            _Comparison = value
            If Me.ExTableLayoutPanel1 IsNot Nothing Then
                ComboBoxComparison.Text = value
            End If
        End Set
    End Property

    Private _Value As String
    Public Property Value As String
        Get
            Return _Value
        End Get
        Set(value As String)
            _Value = value
            If Me.ExTableLayoutPanel1 IsNot Nothing Then
                TextBoxValue.Text = value
            End If
        End Set
    End Property

    Public Property Formula As String

    Public Property NotifyPropertyFilter As Boolean
    Public Property FavoritesList As List(Of String)
    Public Property ProcessEvents As Boolean = True


    Public Sub New(_PropertyFilter As FormPropertyFilter)

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

        Dim UC As New UtilsCommon

        Me.PropertyFilter = _PropertyFilter
        Me.FavoritesList = Form_Main.PropertiesData.GetFavoritesList

        If Not Me.PropertyFilter.ShowAllProps Then
            ComboBoxPropertyName.Items.Add("")
            For Each s As String In FavoritesList
                ComboBoxPropertyName.Items.Add(s)
            Next
        Else
            ComboBoxPropertyName.Items.Add("")
            For Each s As String In Form_Main.PropertiesData.GetAvailableList
                ComboBoxPropertyName.Items.Add(s)
            Next
        End If


        Me.Selected = False
        Me.Variable = ""
        Me.PropertySet = ""
        Me.PropertyName = ""
        Me.Comparison = ""
        Me.Value = ""
        Me.Formula = ""
        Me.NotifyPropertyFilter = True

    End Sub


    Private Sub UpdatePropertySet()
        Dim PropSet As String = ""

        If Form_Main.PropertiesData IsNot Nothing Then
            Dim tmpPropertyData As PropertyData = Form_Main.PropertiesData.GetPropertyData(Me.PropertyName)

            If tmpPropertyData IsNot Nothing Then

                If tmpPropertyData.PropertySetName = PropertyData.PropertySetNameConstants.Custom Then
                    PropSet = "Custom"
                ElseIf tmpPropertyData.IsDuplicate Then
                    PropSet = Me.PropertySet
                ElseIf tmpPropertyData.PropertySetName = PropertyData.PropertySetNameConstants.System Then
                    PropSet = "System"
                End If

            Else
                PropSet = ""
            End If
        End If

        Me.PropertySet = PropSet

    End Sub

    Public Sub Notify()
        If NotifyPropertyFilter Then
            PropertyFilter.UCChanged(Me)
        End If

    End Sub

    Private Function FilterPropertyNames() As List(Of String)
        Dim FilteredList = New List(Of String)
        Dim PropSet As String = ""

        Dim tmpPropList As New List(Of String)

        If Me.PropertyFilter.ShowAllProps Then
            For Each s As String In Form_Main.PropertiesData.GetAvailableList
                tmpPropList.Add(s)
            Next
        Else
            For Each s As String In FavoritesList
                tmpPropList.Add(s)
            Next
        End If

        For Each PropName As String In tmpPropList

            Dim tmpPropertyData As PropertyData = Form_Main.PropertiesData.GetPropertyData(PropName)
            If tmpPropertyData IsNot Nothing Then

                If tmpPropertyData.PropertySetName = PropertyData.PropertySetNameConstants.Custom Then
                    PropSet = "Custom"
                ElseIf tmpPropertyData.IsDuplicate Then
                    PropSet = Me.PropertySet
                ElseIf tmpPropertyData.PropertySetName = PropertyData.PropertySetNameConstants.System Then
                    PropSet = "System"
                End If

            Else
                PropSet = ""
            End If

            If Me.PropertySet = "" Then
                FilteredList.Add(PropName)
            ElseIf PropSet = Me.PropertySet Then
                FilteredList.Add(PropName)
            End If

        Next

        Return FilteredList
    End Function


    Private Sub CheckBoxSelect_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxSelected.CheckedChanged
        Me.Selected = CheckBoxSelected.Checked
        Notify()
    End Sub

    Private Sub ComboBoxPropertySet_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPropertySet.SelectedIndexChanged
        Dim TryIt As Boolean = True

        If Not TryIt Then
            Me.PropertySet = ComboBoxPropertySet.Text
            Notify()
        Else
            If Me.ProcessEvents Then
                Me.ProcessEvents = False

                Me.PropertySet = ComboBoxPropertySet.Text


                Dim PreviousPropertyName = Me.PropertyName
                Dim IsInList As Boolean = False
                ComboBoxPropertyName.Items.Clear()
                For Each PropName As String In FilterPropertyNames()
                    ComboBoxPropertyName.Items.Add(PropName)
                    If PropName = PreviousPropertyName Then IsInList = True
                Next
                If IsInList Then
                    ComboBoxPropertyName.Text = PreviousPropertyName
                Else
                    ComboBoxPropertyName.Text = ""
                End If

                Notify()

                Me.ProcessEvents = True
            End If

        End If

    End Sub

    Private Sub ComboBoxPropertyName_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPropertyName.SelectedIndexChanged

        Dim TryIt As Boolean = True

        If Not TryIt Then
            Me.PropertyName = ComboBoxPropertyName.Text
            UpdatePropertySet()
            Notify()

        Else
            If Me.ProcessEvents Then
                Me.ProcessEvents = False

                Me.PropertyName = ComboBoxPropertyName.Text
                UpdatePropertySet()
                Notify()

                Me.ProcessEvents = True
            End If

        End If
    End Sub

    Private Sub ComboBoxComparison_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxComparison.SelectedIndexChanged
        Me.Comparison = ComboBoxComparison.Text
        Notify()
    End Sub

    Private Sub TextBoxValue_TextChanged(sender As Object, e As EventArgs) Handles TextBoxValue.TextChanged
        Me.Value = TextBoxValue.Text
        Notify()
    End Sub

    Private Sub InsertPropertyToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles InsertPropertyToolStripMenuItem.Click
        Dim TextBox = CType(ContextMenuStrip1.SourceControl, TextBox)
        Dim CaretPosition = TextBox.SelectionStart

        Dim FPP As New FormPropertyPicker

        FPP.ShowDialog()

        If FPP.DialogResult = DialogResult.OK Then
            TextBox.Text = TextBox.Text.Insert(CaretPosition, FPP.PropertyString)

        End If

    End Sub

End Class
