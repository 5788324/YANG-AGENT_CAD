(setvar "CMDDIA" 0)
(setvar "FILEDIA" 0)

(defun yang-layer-report-bool (flag bit)
  (if (= bit (logand flag bit)) "yes" "no")
)

(defun yang-layer-report-count (layer-name / ss)
  (setq ss (ssget "X" (list (cons 8 layer-name))))
  (if ss (sslength ss) 0)
)

(defun yang-layer-report-write (/ dwg-prefix dwg-name report-path handle layer name color flags count total)
  (setq dwg-prefix (getvar "DWGPREFIX"))
  (setq dwg-name (getvar "DWGNAME"))
  (setq report-path (strcat dwg-prefix dwg-name ".layer-report.csv"))
  (setq handle (open report-path "w"))
  (if handle
    (progn
      (write-line "dwg,layer,color,locked,frozen,entity_count" handle)
      (setq total 0)
      (setq layer (tblnext "LAYER" T))
      (while layer
        (setq name (cdr (assoc 2 layer)))
        (setq color (cdr (assoc 62 layer)))
        (setq flags (cdr (assoc 70 layer)))
        (setq count (yang-layer-report-count name))
        (setq total (+ total count))
        (write-line
          (strcat
            dwg-name ","
            name ","
            (itoa color) ","
            (yang-layer-report-bool flags 4) ","
            (yang-layer-report-bool flags 1) ","
            (itoa count)
          )
          handle
        )
        (setq layer (tblnext "LAYER"))
      )
      (write-line (strcat dwg-name ",__TOTAL__,0,no,no," (itoa total)) handle)
      (close handle)
      (princ (strcat "\nYANG AGENT CAD: layer report written to " report-path))
    )
    (princ (strcat "\nYANG AGENT CAD: failed to open report file " report-path))
  )
)

(yang-layer-report-write)
(princ)
