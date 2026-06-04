(setvar "CMDDIA" 0)
(setvar "FILEDIA" 0)

(defun yang-title-block-bool (value)
  (if value "yes" "no")
)

(defun yang-title-block-anonymous-p (name)
  (= "*" (substr name 1 1))
)

(defun yang-title-block-candidate-p (name / upper)
  (setq upper (strcase name))
  (or
    (wcmatch upper "*TITLE*")
    (wcmatch upper "*BORDER*")
    (wcmatch upper "*FRAME*")
    (wcmatch upper "*SHEET*")
    (wcmatch upper "*TK*")
    (wcmatch upper "*TB*")
    (wcmatch name "*图框*")
    (wcmatch name "*标题*")
  )
)

(defun yang-title-block-insert-stats (block-name / ss index total has-attrib ent data)
  (setq ss (ssget "X" (list (cons 0 "INSERT") (cons 2 block-name))))
  (setq total (if ss (sslength ss) 0))
  (setq has-attrib nil)
  (if ss
    (progn
      (setq index 0)
      (while (< index total)
        (setq ent (ssname ss index))
        (setq data (entget ent))
        (if (= 1 (cdr (assoc 66 data))) (setq has-attrib T))
        (setq index (+ index 1))
      )
    )
  )
  (list total has-attrib)
)

(defun yang-title-block-write (/ dwg-prefix dwg-name report-path handle block name flags stats count has-attrib candidate-count)
  (setq dwg-prefix (getvar "DWGPREFIX"))
  (setq dwg-name (getvar "DWGNAME"))
  (setq report-path (strcat dwg-prefix dwg-name ".title-block-candidate-report.csv"))
  (setq handle (open report-path "w"))
  (if handle
    (progn
      (write-line "dwg,block,insert_count,has_attributes,reason" handle)
      (setq candidate-count 0)
      (setq block (tblnext "BLOCK" T))
      (while block
        (setq name (cdr (assoc 2 block)))
        (setq flags (cdr (assoc 70 block)))
        (if
          (and
            (not (yang-title-block-anonymous-p name))
            (= 0 (logand flags 1))
            (= 0 (logand flags 4))
            (yang-title-block-candidate-p name)
          )
          (progn
            (setq stats (yang-title-block-insert-stats name))
            (setq count (car stats))
            (setq has-attrib (cadr stats))
            (if (> count 0)
              (progn
                (setq candidate-count (+ candidate-count 1))
                (write-line
                  (strcat
                    dwg-name ","
                    name ","
                    (itoa count) ","
                    (yang-title-block-bool has-attrib) ","
                    "name_pattern"
                  )
                  handle
                )
              )
            )
          )
        )
        (setq block (tblnext "BLOCK"))
      )
      (write-line (strcat dwg-name ",__TOTAL__," (itoa candidate-count) ",no,candidate_total") handle)
      (close handle)
      (princ (strcat "\nYANG AGENT CAD: title block candidate report written to " report-path))
    )
    (princ (strcat "\nYANG AGENT CAD: failed to open report file " report-path))
  )
)

(yang-title-block-write)
(princ)
