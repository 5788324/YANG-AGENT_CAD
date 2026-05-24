(setvar "CMDDIA" 0)
(setvar "FILEDIA" 0)

(defun yang-block-report-bool (flag bit)
  (if (= bit (logand flag bit)) "yes" "no")
)

(defun yang-block-report-anonymous-p (name)
  (= "*" (substr name 1 1))
)

(defun yang-block-report-count (block-name / ss)
  (setq ss (ssget "X" (list (cons 0 "INSERT") (cons 2 block-name))))
  (if ss (sslength ss) 0)
)

(defun yang-block-report-write (/ dwg-prefix dwg-name report-path handle block name flags count total)
  (setq dwg-prefix (getvar "DWGPREFIX"))
  (setq dwg-name (getvar "DWGNAME"))
  (setq report-path (strcat dwg-prefix dwg-name ".block-report.csv"))
  (setq handle (open report-path "w"))
  (if handle
    (progn
      (write-line "dwg,block,is_xref,is_layout,insert_count" handle)
      (setq total 0)
      (setq block (tblnext "BLOCK" T))
      (while block
        (setq name (cdr (assoc 2 block)))
        (setq flags (cdr (assoc 70 block)))
        (if (not (yang-block-report-anonymous-p name))
          (progn
            (setq count (yang-block-report-count name))
            (setq total (+ total count))
            (write-line
              (strcat
                dwg-name ","
                name ","
                (yang-block-report-bool flags 4) ","
                (yang-block-report-bool flags 1) ","
                (itoa count)
              )
              handle
            )
          )
        )
        (setq block (tblnext "BLOCK"))
      )
      (write-line (strcat dwg-name ",__TOTAL__,no,no," (itoa total)) handle)
      (close handle)
      (princ (strcat "\nYANG AGENT CAD: block report written to " report-path))
    )
    (princ (strcat "\nYANG AGENT CAD: failed to open report file " report-path))
  )
)

(yang-block-report-write)
(princ)
