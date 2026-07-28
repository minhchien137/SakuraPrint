-- Seed/update ZPL template cho tem PDF417 (TemplateKey = 'Pdf417Label') trong bảng
-- SM_Sakura_ZplTemplate — khớp đúng semantics SakuraService.UpsertZplTemplateAsync (update nếu
-- đã có row với TemplateKey này, insert mới nếu chưa có). Run against svn_pentaho.
-- Placeholder trong ZPL bên dưới: {pdf417_1} {pdf417_2} {pdf417_3} {pdf417_4} — logic build/điền
-- giá trị sẽ được bổ sung sau.
IF EXISTS (SELECT 1 FROM dbo.SM_Sakura_ZplTemplate WHERE TemplateKey = 'Pdf417Label')
BEGIN
    UPDATE dbo.SM_Sakura_ZplTemplate
    SET ZplContent = N'^XA
~TA000
~JSN
^LT0
^MNW
^MTT
^PON
^PMN
^LH0,0
^JMA
^PR2,2
~SD15
^JUS
^LRN
^CI27
^PA0,1,1,0
^XZ
^XA
^MMT
^PW2400
^LL3600
^LS0
^BY26,52^FT80,873^B7N,52,2,,,N
^FH\^FD{pdf417_1}^FS
^BY26,52^FT80,1736^B7N,52,2,,,N
^FH\^FD{pdf417_2}^FS
^BY26,52^FT80,2619^B7N,52,2,,,N
^FH\^FD{pdf417_3}^FS
^BY26,52^FT80,3503^B7N,52,2,,,N
^FH\^FD{pdf417_4}^FS
^PQ1,0,1,Y
^XZ',
        UpdatedAt = GETDATE(),
        UpdatedBy = N'claude-code'
    WHERE TemplateKey = 'Pdf417Label';
END
ELSE
BEGIN
    INSERT INTO dbo.SM_Sakura_ZplTemplate (TemplateKey, Name, ZplContent, IsActive, UpdatedAt, UpdatedBy)
    VALUES (N'Pdf417Label', N'Pdf417Label', N'^XA
~TA000
~JSN
^LT0
^MNW
^MTT
^PON
^PMN
^LH0,0
^JMA
^PR2,2
~SD15
^JUS
^LRN
^CI27
^PA0,1,1,0
^XZ
^XA
^MMT
^PW2400
^LL3600
^LS0
^BY26,52^FT80,873^B7N,52,2,,,N
^FH\^FD{pdf417_1}^FS
^BY26,52^FT80,1736^B7N,52,2,,,N
^FH\^FD{pdf417_2}^FS
^BY26,52^FT80,2619^B7N,52,2,,,N
^FH\^FD{pdf417_3}^FS
^BY26,52^FT80,3503^B7N,52,2,,,N
^FH\^FD{pdf417_4}^FS
^PQ1,0,1,Y
^XZ', 1, GETDATE(), N'claude-code');
END
GO
