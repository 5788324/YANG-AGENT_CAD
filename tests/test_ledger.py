from datetime import datetime

from yang_cad_agent.ledger import new_task_id


def test_new_task_id_has_date_prefix():
    task_id = new_task_id(datetime(2026, 5, 23, 9, 8, 7))
    assert task_id.startswith("20260523-090807-")

