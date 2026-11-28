/**
 * Scam Sense: Think Before You Click — Student Survey Questionnaire
 * Google Apps Script that creates the Google Form in YOUR Google account.
 *
 * HOW TO USE
 * 1. Go to https://script.google.com and click "New project".
 * 2. Delete the sample code, paste this whole file, and click Save.
 * 3. Select the function "createScamSenseSurvey" and click Run.
 * 4. Allow the permission request (it only needs access to create Google Forms).
 * 5. Open View > Logs (or Execution log): it shows the edit link and the link to share with respondents.
 *
 * The form has the same 30 items as ScamSense_Student_Survey_Questionnaire.docx.
 * It does not collect email addresses or names.
 */

var LIKERT = ['5 - Strongly Agree', '4 - Agree', '3 - Neutral', '2 - Disagree', '1 - Strongly Disagree'];

function createScamSenseSurvey() {
  var form = FormApp.create('Scam Sense: Student Survey Questionnaire');
  form.setTitle('Scam Sense: Think Before You Click — Student Survey Questionnaire');
  form.setDescription(
    'A Game-Based Learning Approach to Cybersecurity Awareness\n' +
    'Capstone Project, Group 17, Bachelor of Science in Information Technology\n' +
    'University of Perpetual Help System Laguna\n\n' +
    'Dear Respondent,\n\n' +
    'We are BSIT students of the University of Perpetual Help System Laguna conducting a capstone project titled ' +
    '"Scam Sense: Think Before You Click". Scam Sense is a proposed 3D educational game in which the player acts as an ' +
    'investigator who examines messages and decides whether they are legitimate or fraudulent.\n\n' +
    'This survey aims to gather information about students\' current awareness of online scams, their ability to recognise ' +
    'suspicious messages, their online safety practices, and their preferred ways of learning. Your answers will guide the ' +
    'design of the game. The survey takes about 10 to 15 minutes to complete.\n\n' +
    'INSTRUCTIONS: Read each question carefully and answer honestly. There are no right or wrong answers. ' +
    'For rating items, use the scale 5 - Strongly Agree, 4 - Agree, 3 - Neutral, 2 - Disagree, 1 - Strongly Disagree.'
  );
  form.setCollectEmail(false);
  form.setLimitOneResponsePerUser(false);
  form.setProgressBar(true);
  form.setConfirmationMessage('Thank you for your time and participation. — Scam Sense Capstone Team, Group 17, BSIT, UPHSL');

  // ---------- Consent ----------
  form.addSectionHeaderItem()
    .setTitle('Consent Statement')
    .setHelpText('Your participation is voluntary. You may skip any question or stop at any time without any consequence. ' +
      'This survey does not ask for your name, contact details or account information, and your answers will be kept ' +
      'confidential in accordance with the Data Privacy Act of 2012 (RA 10173). Results will be reported only as group ' +
      'totals and used only for academic purposes.');
  var consent = form.addCheckboxItem()
    .setTitle('Consent')
    .setRequired(true);
  consent.setChoices([consent.createChoice('I have read and understood the information above, and I voluntarily agree to answer this survey.')]);

  // ---------- Part I ----------
  page(form, 'Part I. Respondent Profile', '');
  mc(form, '1. Age', ['15 or below', '16 to 18', '19 to 21', '22 or above']);
  mc(form, '2. Year level', ['Grade 11', 'Grade 12', '1st year college', '2nd year college', '3rd year college', '4th year college or above']);
  form.addTextItem().setTitle('3. Academic program / strand').setHelpText('e.g. BSIT, BSBA, STEM, ABM');

  // ---------- Part II ----------
  page(form, 'Part II. Awareness of Online Scams', '');
  mc(form, '4. Before this survey, had you heard of online scams, such as fake messages or websites made to steal money, passwords or personal information?', ['Yes', 'No']);
  cb(form, '5. Where have you learned or heard about online scams? (tick all that apply)', [
    'School (classes, seminars or announcements)', 'Family or friends', 'News on TV, radio or online',
    'Social media posts or videos', 'Warnings from banks, e-wallets or mobile networks', 'My own experience',
    'I have not heard about online scams']);
  mc(form, '6. In the past 12 months, have you received a message, call or email that you believed was a scam?', ['Yes', 'No', 'Not sure']);
  cb(form, '7. Through which of the following have you received suspicious messages? (tick all that apply)', [
    'Text message (SMS)', 'Phone call', 'Email', 'Facebook, Messenger or other social media', 'Online shopping apps',
    'Online games', 'Websites or pop-up advertisements', 'I have not received any']);
  mc(form, '8. Have you, or someone you know personally, lost money, an account or personal information because of an online scam?', ['Yes', 'No', 'Prefer not to say']);
  grid(form, 'Items 9 to 10. How much do you agree with each statement?', [
    '9. I know that scams can come through text messages, calls, emails, social media and websites.',
    '10. I know that a fake website can be made to look almost the same as a real one.']);

  // ---------- Part III ----------
  page(form, 'Part III. Knowledge and Recognition', '');
  grid(form, 'Items 11 to 14. How much do you agree with each statement?', [
    '11. I can tell when a message or email is asking for information in a suspicious way.',
    '12. I can tell when a link may lead to a fake or harmful website.',
    '13. I check whether a sender\'s name, number or email address looks real before I trust a message.',
    '14. I know that real banks and e-wallets do not ask for my one-time PIN (OTP) or password through text or calls.']);
  mc(form, '15. You receive this text: "Your parcel is on hold. Pay a PHP 50 delivery fee within 24 hours at this link to avoid return." You are not expecting a parcel. What would you most likely do?', [
    'Click the link and pay the fee', 'Reply to ask for more details', 'Ignore or delete the message',
    'Check with the courier through its official app, website or hotline', 'I am not sure what to do']);
  cb(form, '16. Which of the following could be a warning sign that a message is a scam? (tick all that apply)', [
    'It says you must act right away or something bad will happen', 'It asks for your password, PIN or one-time code',
    'It offers a prize, job or deal that seems too good to be true',
    'The sender\'s address or link has misspellings or extra words',
    'It asks you to pay or chat outside the official app or website', 'None of the above']);

  // ---------- Part IV ----------
  page(form, 'Part IV. Online Safety Practices', '');
  grid(form, 'Items 17 to 20. How much do you agree with each statement?', [
    '17. I avoid clicking links in messages from unknown senders.',
    '18. I do not share my passwords, PINs or one-time codes with anyone.',
    '19. I use different passwords for my important accounts.',
    '20. I block or report suspicious accounts, numbers or messages.']);
  mc(form, '21. Do you use two-step verification (an extra code or approval needed when logging in) on your important accounts?', ['Yes', 'No', 'I do not know what this is']);
  mc(form, '22. What do you usually do FIRST when you receive a suspicious message?', [
    'Reply to the sender', 'Click the link to find out more', 'Ignore or delete it',
    'Ask a family member, friend or teacher', 'Check with the company through its official app, website or hotline',
    'Report or block the sender']);

  // ---------- Part V ----------
  page(form, 'Part V. Learning Preferences and Educational Game Interest', '');
  mc(form, '23. Which way of learning about online safety do you prefer most?', [
    'Lectures or classroom discussions', 'Reading materials (handouts, articles, infographics)', 'Videos',
    'Interactive games or simulations', 'Seminars or workshops']);
  mc(form, '24. Have you ever used an educational game to learn a school subject or skill?', ['Yes', 'No']);
  grid(form, 'Items 25 to 26. How much do you agree with each statement?', [
    '25. I would be interested in learning about online safety through an interactive game.',
    '26. A game where I investigate messages like a detective would make learning about scams more interesting for me.']);

  // ---------- Part VI ----------
  page(form, 'Part VI. Expectations for Scam Sense',
    'Scam Sense is a proposed 3D game in which you act as an investigator, examine messages such as texts, emails and links, ' +
    'and decide whether each one is legitimate or a scam. After each decision, the game explains the warning signs. ' +
    'Based on this description, please rate the following.');
  grid(form, 'Items 27 to 30. How much do you agree with each statement?', [
    '27. A game like Scam Sense could help students recognise suspicious messages and links.',
    '28. A game like Scam Sense should be easy to play, even for students who rarely play games.',
    '29. Explanations after each decision would help me understand why a message is a scam.',
    '30. I would be willing to try Scam Sense if it became available.']);

  Logger.log('Edit the form:  ' + form.getEditUrl());
  Logger.log('Share with respondents:  ' + form.getPublishedUrl());
}

// ---------- helpers ----------
function page(form, title, help) {
  var p = form.addPageBreakItem().setTitle(title);
  if (help) p.setHelpText(help);
  return p;
}
function mc(form, title, choices) {
  var item = form.addMultipleChoiceItem().setTitle(title);
  item.setChoiceValues(choices);
  return item;
}
function cb(form, title, choices) {
  var item = form.addCheckboxItem().setTitle(title);
  item.setChoiceValues(choices);
  return item;
}
function grid(form, title, rows) {
  return form.addGridItem()
    .setTitle(title)
    .setHelpText('5 - Strongly Agree, 4 - Agree, 3 - Neutral, 2 - Disagree, 1 - Strongly Disagree')
    .setRows(rows)
    .setColumns(LIKERT);
}
