   M S B A M L   `   `   `  ����F  BTebegrammmm, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null_4clr-namespace:Tebegrammmm.ChatsFoldersRedactsWindows&Tebegrammmm.ChatsFoldersRedactsWindows  5   	   ;"clr-namespace:Tebegrammmm.ControlsTebegrammmm.Controls  5   	   G    ATebegrammmm.ChatsFoldersRedactsWindows.RedactFolderContatcsWindow   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   Flocal4clr-namespace:Tebegrammmm.ChatsFoldersRedactsWindows     5   	   5local1"clr-namespace:Tebegrammmm.Controls     5   	     � Title$  
Папки��5	   	   $	��400��6   $	��350��6$    � WindowStartupLocation$ CenterScreen=�60   .��5
      � $��10q�6    � RowDefinitions 5   
   �� 5      $
��auto �6   �� 5      $
��auto �6   �� 5      $
��auto �6   �� 5      $
��auto �6   .+�5   
   �� $7��1Редактирование контактоов��6   $��20(�65   �� 5   
   $��2��6   $	��250��6$   $
��0,10q�61   .��5      � $��2��6    5      �� 5      $
��auto �6$   �� 5      $
��auto �6$   �� 5      $
��auto �6$   .+�5      �  5      �� 5      �� 5      .+�5      �� $��Контакты ��6    �� 5      -   5         ��Name$ LBMyContacts��5      $��1��64   ��5       �� 5!      .R�5"   "   �  � ColumnDefinitions 5#   &   �� 5$   *   $
��auto �6;   �� 5%   *   .+�5'   &   '   !Tebegrammmm.Controls.UserControl1  �� 5(   &   $��1��6F   ��60   �� *Name+6F   5)   #   5*      5,      5-      � 5.      $��1��6    5/      �� 50      �� 51      .+�53      �� $+��%Добавленые контакты��6    �� 54      -   5        $ LBFolderContacts��54      $��1��68   ��55      �� 56      .R�57   "   �  58   &   �� 59   *   $
��auto �6;   �� 5:   *   .+�5<   &     �� 5=   &   $��1��6F   ��60   �� *Name+6F   5>   #   5?      5A      5B      5C      5D      �� 5E   
   -   5        $��3��5E      $��30��6   $	��100��6*   $��
Выход��66   6F   5F      5G      