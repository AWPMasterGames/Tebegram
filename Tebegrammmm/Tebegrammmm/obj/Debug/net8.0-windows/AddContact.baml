   M S B A M L   `   `   `  ����F  BTebegrammmm, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:TebegrammmmTebegrammmm  5   	       Tebegrammmm.AddContact   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   +localclr-namespace:Tebegrammmm     5   	     � Title$  Контакт��5   	   $	��350��6   $	��400��6&    � WindowStartupLocation$ CenterScreen=�62   .��5	      �  � ColumnDefinitions 5
   
   �� 5      �� 5      $��5* �6   �� 5       � RowDefinitions 5   
   �� 5      �� 5      $
��auto �6   �� 5      $
��auto �6   �� 5      $
��auto �6   �� 5      $
��auto �6   .+�5   
   �� -   5         ��Name$ TBTitle��5      $%��Добавить контакт��6%   $��25(�6=   $��Center=�6K   $��Center=�6h   $��1��6�   � 5   
   $��1��6   $��1��6   $
��0,10q�6,    5      �� 5      $
��auto �6    �� 5      $
��auto �6    .+�5      �� $��Имя��6   $
��10,0q�6#   
�������$��Bottom=�6C   �� 5      -   5         ��Name$ TBName��5      $�� ��6&   $��20(�6.   $��1��6<   5      � 5   
   $��1��6   $��2��6   $
��0,10q�6,    5       �� 5!      $
��auto �6    �� 5"      $
��auto �6    .+�5$      �� $��	IP Adress��6   $
��10,0q�6)   
�������$��Bottom=�6I   �� 5%      -   5        $ 
TBIPAdress��5%      $�� ��6*   $��20(�62   $��1��6@   5&      � 5'   
   $��1��6   $��3��6   $
��0,10q�6,    5(      �� 5)      $
��auto �6    �� 5*      $
��auto �6    .+�5,      �� $
��Port��6   $
��10,0q�6$   
�������$��Bottom=�6D   �� 5-      -   5        $ TBPort��5-      $��20(�6&   $��1��64   5.      �� 5/   
   $��4��6   $��1��6"    �� Orientation$ 
Horizontal=�62   $��Center=�6K   .+�50      �� -   5        $��Добавить��50      $��25��6(   $	��100��64   $��10q�6@   6L   �� 51      -   5        $��Отмена��51      $��25��6&   $��75��62   $��10q�6=   6I   52      53      54      