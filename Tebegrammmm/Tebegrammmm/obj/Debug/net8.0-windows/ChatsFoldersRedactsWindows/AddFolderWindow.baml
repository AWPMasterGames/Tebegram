   M S B A M L   `   `   `  ����F  BTebegrammmm, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null_4clr-namespace:Tebegrammmm.ChatsFoldersRedactsWindows&Tebegrammmm.ChatsFoldersRedactsWindows  5   	   ;"clr-namespace:Tebegrammmm.ControlsTebegrammmm.Controls  5   	   <    6Tebegrammmm.ChatsFoldersRedactsWindows.AddFolderWindow   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   Flocal4clr-namespace:Tebegrammmm.ChatsFoldersRedactsWindows     5   	   5local1"clr-namespace:Tebegrammmm.Controls     5   	     � Title$  
Папки��5	   	   $	��450��6   $	��350��6$    � WindowStartupLocation$ CenterScreen=�60   .��5
      � $��10q�6    � RowDefinitions 5   
   �� 5      $
��auto �6   �� 5      $
��auto �6   �� 5      $
��auto �6   �� 5      $
��auto �6   .+�5   
   �� $��Новая папка��6   $��20(�6'   � 5   
   $��1��6   $��0,20,0,0q�6    � ColumnDefinitions 5      �� 5      �� 5      $
��auto �6#    5      �� 5      $
��auto �6    �� 5      $
��auto �6    .+�5      �� $!��Название папки��6   $��13(�6.   
�������6<   �� 5      -   5        $��TBoxFolderName��5      $��1��6,   $��15(�69   $��2��6G   �� 5      $��1��6   $��1��6%   $
��📁��65   $��20(�6?   5      �� 5   
   $��2��6   $	��250��6$   .��5!      � $��2��6    5"      �� 5#      $
��auto �6$   �� 5$      $
��auto �6$   �� 5%      $
��auto �6$   .+�5'      �  5(      �� 5)      �� 5*      .+�5,      �� $��Контакты ��6    �� 5-      -   5         ��Name$ LBMyContacts��5-      $��1��64   ��5.      �� 5/      .R�50   "   �  51   &   �� 52   *   $
��auto �6;   �� 53   *   .+�55   &   '   !Tebegrammmm.Controls.UserControl1  �� 56   &   $��1��6F   ��60   �� *Name+6F   57   #   58      5:      5;      � 5<      $��1��6    5=      �� 5>      �� 5?      .+�5A      �� $+��%Добавленые контакты��6    �� 5B      -   5        $ LBFolderContacts��5B      $��1��68   ��5C      �� 5D      .R�5E   "   �  5F   &   �� 5G   *   $
��auto �6;   �� 5H   *   .+�5J   &     �� 5K   &   $��1��6F   ��60   �� *Name+6F   �� 5M   &   -   5        $��3��5M   -   $��30��5N   )   $��30��64   
������ $��Center=�5P   )   ��5R   )   �� .��5S   *   �� $	��✕��64   $��16(�6=   ���#FF00006K   5T   '   5U   #   5V      5X      5Y      5Z      5[      � 5\   
   $��3��6   $��Bottom=�6    5]      �� 5^      �� 5_      .+�5a      �� -   5        $��30��5a      $	��100��6!   $��Сохранить��6-   $��	32,0,33,0q�6A   $	��Top=�6T   6l   �� 5b      -   5        $��1��5b      $��30��6%   $	��100��61   $��Отмена��6=   6N   5c      5d      5e      