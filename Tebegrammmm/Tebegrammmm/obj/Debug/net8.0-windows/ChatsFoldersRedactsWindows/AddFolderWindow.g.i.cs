﻿#pragma checksum "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "F03ED397772C4B820C0369A593EDDA4E16A18FC9"
//------------------------------------------------------------------------------
// <auto-generated>
//     Этот код создан программой.
//     Исполняемая версия:4.0.30319.42000
//
//     Изменения в этом файле могут привести к неправильной работе и будут потеряны в случае
//     повторной генерации кода.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using Tebegrammmm.ChatsFoldersRedactsWindows;
using Tebegrammmm.Controls;


namespace Tebegrammmm.ChatsFoldersRedactsWindows {
    
    
    /// <summary>
    /// AddFolderWindow
    /// </summary>
    public partial class AddFolderWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector, System.Windows.Markup.IStyleConnector {
        
        
        #line 28 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBox TBoxFolderName;
        
        #line default
        #line hidden
        
        
        #line 45 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ListBox LBMyContacts;
        
        #line default
        #line hidden
        
        
        #line 66 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ListBox LBFolderContacts;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "8.0.8.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/Tebegrammmm;component/chatsfoldersredactswindows/addfolderwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "8.0.8.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.TBoxFolderName = ((System.Windows.Controls.TextBox)(target));
            return;
            case 2:
            this.LBMyContacts = ((System.Windows.Controls.ListBox)(target));
            
            #line 45 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
            this.LBMyContacts.SelectionChanged += new System.Windows.Controls.SelectionChangedEventHandler(this.LBMyContacts_SelectionChanged);
            
            #line default
            #line hidden
            return;
            case 3:
            this.LBFolderContacts = ((System.Windows.Controls.ListBox)(target));
            
            #line 66 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
            this.LBFolderContacts.SelectionChanged += new System.Windows.Controls.SelectionChangedEventHandler(this.LBFolderContacts_SelectionChanged);
            
            #line default
            #line hidden
            return;
            case 5:
            
            #line 97 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.Button_Click);
            
            #line default
            #line hidden
            return;
            case 6:
            
            #line 98 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.Button_Click_1);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "8.0.8.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        void System.Windows.Markup.IStyleConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 4:
            
            #line 81 "..\..\..\..\ChatsFoldersRedactsWindows\AddFolderWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.Remove_Contact);
            
            #line default
            #line hidden
            break;
            }
        }
    }
}

